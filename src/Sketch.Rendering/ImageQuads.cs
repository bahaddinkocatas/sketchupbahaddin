using Silk.NET.OpenGL;
using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Rendering;

/// <summary>Decodes image files (PNG, JPG, ...) into BGRA pixels; supplied by the UI layer.</summary>
public interface IImageDecoder
{
    (byte[] Bgra, int Width, int Height) Decode(byte[] fileBytes);
}

/// <summary>Draws imported images as textured rectangles (one texture per distinct image).</summary>
internal sealed class ImageQuads : IDisposable
{
    private const string Vertex = """
        #version 330 core
        layout(location = 0) in vec3 aPos;
        layout(location = 1) in vec2 aUv;
        uniform mat4 uViewProj;
        out vec2 vUv;
        void main() {
            gl_Position = uViewProj * vec4(aPos, 1.0);
            vUv = aUv;
        }
        """;

    private const string Fragment = """
        #version 330 core
        in vec2 vUv;
        uniform sampler2D uTex;
        uniform float uFade;
        out vec4 FragColor;
        void main() {
            vec4 c = texture(uTex, vUv);
            FragColor = vec4(mix(c.rgb, vec3(1.0), uFade), c.a);
        }
        """;

    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly uint _vao, _vbo;
    private readonly Dictionary<string, uint> _textures = new();
    private readonly List<(ImageEntity Image, bool Faded)> _items = new();

    public IImageDecoder? Decoder { get; set; }

    public ImageQuads(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Vertex, Fragment);
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        const uint stride = 5 * sizeof(float);
        unsafe
        {
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        }
        gl.BindVertexArray(0);
    }

    public void Clear() => _items.Clear();

    /// <summary>Frees the textures of pictures that no longer exist in the model.</summary>
    public void Prune(IEnumerable<string> liveKeys)
    {
        var live = liveKeys.ToHashSet();
        foreach (var (key, tex) in _textures.Where(kv => !live.Contains(kv.Key)).ToList())
        {
            _gl.DeleteTexture(tex);
            _textures.Remove(key);
        }
    }

    public void Add(ImageEntity image, bool faded) => _items.Add((image, faded));

    private uint? Texture(string key, ImageStore store)
    {
        if (_textures.TryGetValue(key, out var tex)) return tex;
        if (Decoder is null || store.Get(key) is not { } bytes) return null;
        (byte[] Bgra, int Width, int Height) decoded;
        try
        {
            decoded = Decoder.Decode(bytes);
        }
        catch
        {
            return null; // a broken image must not break drawing
        }
        tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        unsafe
        {
            fixed (byte* p = decoded.Bgra)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)decoded.Width, (uint)decoded.Height, 0,
                    PixelFormat.Bgra, PixelType.UnsignedByte, p);
        }
        _gl.GenerateMipmap(TextureTarget.Texture2D);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        _textures[key] = tex;
        return tex;
    }

    public unsafe void Draw(float[] viewProj, ImageStore? store)
    {
        if (_items.Count == 0 || store is null) return;
        _shader.Use();
        _shader.Set("uViewProj", viewProj);
        _shader.Set("uTex", 0);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        var data = new float[6 * 5];
        foreach (var (img, faded) in _items)
        {
            if (Texture(img.ImageKey, store) is not { } tex) continue;
            var c = img.Corners;
            // Image row 0 is the top: V runs up the picture.
            (Vector3d P, float U, float V)[] verts =
            {
                (c[0], 0, 1), (c[1], 1, 1), (c[2], 1, 0), (c[0], 0, 1), (c[2], 1, 0), (c[3], 0, 0),
            };
            for (var i = 0; i < 6; i++)
            {
                data[i * 5] = (float)verts[i].P.X;
                data[i * 5 + 1] = (float)verts[i].P.Y;
                data[i * 5 + 2] = (float)verts[i].P.Z;
                data[i * 5 + 3] = verts[i].U;
                data[i * 5 + 4] = verts[i].V;
            }
            fixed (float* p = data)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), p, BufferUsageARB.StreamDraw);
            _shader.Set("uFade", faded ? 0.65f : 0f);
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        foreach (var t in _textures.Values) _gl.DeleteTexture(t);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _shader.Dispose();
    }
}
