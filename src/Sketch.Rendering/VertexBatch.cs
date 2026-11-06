using Silk.NET.OpenGL;

namespace Sketch.Rendering;

/// <summary>
/// Interleaved float vertex data uploaded to a dynamic buffer. The attribute layout is given as a list of
/// component counts (e.g. {3, 4, 1, 1} = position, color, width, dashed).
/// </summary>
internal sealed class VertexBatch : IDisposable
{
    private readonly GL _gl;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly int _floatsPerVertex;
    private readonly PrimitiveType _primitive;
    private int _capacityBytes;
    private bool _dirty = true;

    public List<float> Data { get; } = new();

    public VertexBatch(GL gl, PrimitiveType primitive, params int[] attributeSizes)
    {
        _gl = gl;
        _primitive = primitive;
        _floatsPerVertex = attributeSizes.Sum();
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        var stride = (uint)(_floatsPerVertex * sizeof(float));
        var offset = 0;
        for (uint i = 0; i < attributeSizes.Length; i++)
        {
            gl.EnableVertexAttribArray(i);
            unsafe
            {
                gl.VertexAttribPointer(i, attributeSizes[i], VertexAttribPointerType.Float, false, stride, (void*)(offset * sizeof(float)));
            }
            offset += attributeSizes[(int)i];
        }
        gl.BindVertexArray(0);
    }

    public int VertexCount => Data.Count / _floatsPerVertex;

    public void Clear()
    {
        Data.Clear();
        _dirty = true;
    }

    public void MarkDirty() => _dirty = true;

    public unsafe void Draw()
    {
        if (Data.Count == 0) return;
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        if (_dirty)
        {
            var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(Data);
            var bytes = span.Length * sizeof(float);
            fixed (float* p = span)
            {
                if (bytes > _capacityBytes)
                {
                    _capacityBytes = Math.Max(bytes, _capacityBytes * 2);
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)_capacityBytes, null, BufferUsageARB.DynamicDraw);
                }
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)bytes, p);
            }
            _dirty = false;
        }
        _gl.DrawArrays(_primitive, 0, (uint)VertexCount);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
    }
}
