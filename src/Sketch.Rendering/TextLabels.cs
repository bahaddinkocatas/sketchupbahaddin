using Silk.NET.OpenGL;
using Sketch.Geometry;
using Sketch.Tools;

namespace Sketch.Rendering;

/// <summary>Turns a string into a premultiplied BGRA image (supplied by the UI layer, which owns fonts).</summary>
public interface ITextRasterizer
{
    (byte[] Bgra, int Width, int Height) Rasterize(string text, DrawColor color, bool boxed);
}

/// <summary>
/// Screen-facing text labels anchored at world points. Each distinct text/colour is rasterised once and kept as
/// a texture; labels are drawn at a constant pixel size. Tool labels are always on top; model labels (texts,
/// dimensions) are depth tested at their anchor, so geometry in front of the anchor hides them.
/// </summary>
internal sealed class TextLabels : IDisposable
{
    private const string Vertex = """
        #version 330 core
        layout(location = 0) in vec3 aAnchor;
        layout(location = 1) in vec2 aOffset;   // pixels, y up
        layout(location = 2) in vec2 aUv;
        uniform mat4 uViewProj;
        uniform vec2 uViewport;
        uniform int uDepthTested;
        out vec2 vUv;
        void main() {
            vec4 c = uViewProj * vec4(aAnchor, 1.0);
            if (c.w <= 0.0) { gl_Position = vec4(2.0, 2.0, 2.0, 1.0); vUv = aUv; return; }
            vec2 ndc = c.xy / c.w + aOffset * 2.0 / uViewport;
            // At the anchor's depth, nudged towards the eye so a label on a face is not hidden by that face.
            float z = uDepthTested == 1 ? clamp(c.z / c.w - 0.002, -1.0, 1.0) : -1.0;
            gl_Position = vec4(ndc, z, 1.0);
            vUv = aUv;
        }
        """;

    private const string Fragment = """
        #version 330 core
        in vec2 vUv;
        uniform sampler2D uTex;
        out vec4 FragColor;
        void main() { FragColor = texture(uTex, vUv); }
        """;

    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly uint _vao, _vbo;
    private readonly Dictionary<(string, DrawColor, bool), (uint Texture, int W, int H)> _cache = new();
    private readonly List<(Vector3d Anchor, string Text, DrawColor Color, double Dx, double Dy, bool Boxed, bool DepthTested)> _labels = new();

    public ITextRasterizer? Rasterizer { get; set; }

    public TextLabels(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Vertex, Fragment);
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        const uint stride = 7 * sizeof(float);
        unsafe
        {
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
            gl.EnableVertexAttribArray(2);
            gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(5 * sizeof(float)));
        }
        gl.BindVertexArray(0);
    }

    public void Clear() => _labels.Clear();

    /// <summary>Queues a label centred on the anchor, shifted by (dx, dy) pixels (y down).</summary>
    public void Add(Vector3d anchor, string text, DrawColor color, double dx = 0, double dy = 0, bool boxed = false, bool depthTested = false)
    {
        if (!string.IsNullOrEmpty(text)) _labels.Add((anchor, text, color, dx, dy, boxed, depthTested));
    }

    private (uint Texture, int W, int H)? TextureFor(string text, DrawColor color, bool boxed)
    {
        if (_cache.TryGetValue((text, color, boxed), out var t)) return t;
        if (Rasterizer is null) return null;
        var (pixels, w, h) = Rasterizer.Rasterize(text, color, boxed);
        if (w == 0 || h == 0) return null;
        var tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        unsafe
        {
            fixed (byte* p = pixels)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h, 0, PixelFormat.Bgra, PixelType.UnsignedByte, p);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        // Keep the cache bounded (texts change while measuring).
        if (_cache.Count > 4000)
        {
            foreach (var old in _cache.Values) _gl.DeleteTexture(old.Texture);
            _cache.Clear();
        }
        var entry = (tex, w, h);
        _cache[(text, color, boxed)] = entry;
        return entry;
    }

    public unsafe void Draw(float[] viewProj, int width, int height)
    {
        if (_labels.Count == 0 || Rasterizer is null) return;
        _shader.Use();
        _shader.Set("uViewProj", viewProj);
        _shader.Set("uViewport", width, height);
        _shader.Set("uTex", 0);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthMask(false);
        _gl.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha); // premultiplied alpha
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        var data = new float[6 * 7];
        // Model labels that would cover one already drawn are skipped (decluttering, as in CAD programs):
        // zoomed out only non-overlapping labels show, zooming in reveals the rest. Tool feedback always shows.
        var taken = new Dictionary<(int, int), List<(double X0, double Y0, double X1, double Y1)>>();
        var viewProjM = viewProj;
        foreach (var (anchor, text, color, dx, dy, boxed, depthTested) in _labels)
        {
            if (TextureFor(text, color, boxed) is not { } t) continue;
            _shader.Set("uDepthTested", depthTested ? 1 : 0);
            // Pixel-snapped quad centred on the anchor (y up in offsets).
            var x0 = Math.Round(dx - t.W / 2.0);
            var y0 = Math.Round(-dy - t.H / 2.0);
            var x1 = x0 + t.W;
            var y1 = y0 + t.H;
            if (!boxed && ScreenOf(viewProjM, anchor, width, height) is { } c)
            {
                var r = (c.X + x0, c.Y + y0, c.X + x1, c.Y + y1);
                if (Overlaps(taken, r)) continue;
                Take(taken, r);
            }
            var corners = new[] { (x0, y0, 0f, 1f), (x1, y0, 1f, 1f), (x1, y1, 1f, 0f), (x0, y0, 0f, 1f), (x1, y1, 1f, 0f), (x0, y1, 0f, 0f) };
            for (var i = 0; i < 6; i++)
            {
                var (ox, oy, u, v) = corners[i];
                data[i * 7] = (float)anchor.X;
                data[i * 7 + 1] = (float)anchor.Y;
                data[i * 7 + 2] = (float)anchor.Z;
                data[i * 7 + 3] = (float)ox;
                data[i * 7 + 4] = (float)oy;
                data[i * 7 + 5] = u;
                data[i * 7 + 6] = v;
            }
            fixed (float* p = data)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), p, BufferUsageARB.StreamDraw);
            _gl.BindTexture(TextureTarget.Texture2D, t.Texture);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }
        _gl.BindVertexArray(0);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.DepthMask(true);
    }

    /// <summary>Anchor in screen pixels (y up), or null behind the eye.</summary>
    private static (double X, double Y)? ScreenOf(float[] m, Vector3d p, int width, int height)
    {
        // Column-major view-projection matrix.
        var x = m[0] * p.X + m[4] * p.Y + m[8] * p.Z + m[12];
        var y = m[1] * p.X + m[5] * p.Y + m[9] * p.Z + m[13];
        var w = m[3] * p.X + m[7] * p.Y + m[11] * p.Z + m[15];
        if (w <= 0) return null;
        return ((x / w + 1) / 2 * width, (y / w + 1) / 2 * height);
    }

    private const int LabelCell = 64;

    private static IEnumerable<(int, int)> Cells((double X0, double Y0, double X1, double Y1) r)
    {
        for (var i = (int)Math.Floor(r.X0 / LabelCell); i <= (int)Math.Floor(r.X1 / LabelCell); i++)
        for (var j = (int)Math.Floor(r.Y0 / LabelCell); j <= (int)Math.Floor(r.Y1 / LabelCell); j++)
            yield return (i, j);
    }

    private static bool Overlaps(Dictionary<(int, int), List<(double X0, double Y0, double X1, double Y1)>> taken, (double X0, double Y0, double X1, double Y1) r)
    {
        foreach (var cell in Cells(r))
            if (taken.TryGetValue(cell, out var list))
                foreach (var o in list)
                    if (r.X0 < o.X1 && o.X0 < r.X1 && r.Y0 < o.Y1 && o.Y0 < r.Y1) return true;
        return false;
    }

    private static void Take(Dictionary<(int, int), List<(double X0, double Y0, double X1, double Y1)>> taken, (double X0, double Y0, double X1, double Y1) r)
    {
        foreach (var cell in Cells(r))
        {
            if (!taken.TryGetValue(cell, out var list)) taken[cell] = list = new();
            list.Add(r);
        }
    }

    public void Dispose()
    {
        foreach (var t in _cache.Values) _gl.DeleteTexture(t.Texture);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _shader.Dispose();
    }
}
