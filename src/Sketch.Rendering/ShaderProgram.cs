using Silk.NET.OpenGL;

namespace Sketch.Rendering;

internal sealed class ShaderProgram : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, int> _uniforms = new();

    public uint Handle { get; }

    public ShaderProgram(GL gl, string vertexSource, string fragmentSource, string? geometrySource = null)
    {
        _gl = gl;
        var shaders = new List<uint>
        {
            Compile(ShaderType.VertexShader, vertexSource),
            Compile(ShaderType.FragmentShader, fragmentSource),
        };
        if (geometrySource is not null) shaders.Add(Compile(ShaderType.GeometryShader, geometrySource));

        Handle = gl.CreateProgram();
        foreach (var s in shaders) gl.AttachShader(Handle, s);
        gl.LinkProgram(Handle);
        gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out var ok);
        if (ok == 0) throw new InvalidOperationException("Shader link failed: " + gl.GetProgramInfoLog(Handle));
        foreach (var s in shaders)
        {
            gl.DetachShader(Handle, s);
            gl.DeleteShader(s);
        }
    }

    private uint Compile(ShaderType type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var ok);
        if (ok == 0) throw new InvalidOperationException($"{type} compile failed: " + _gl.GetShaderInfoLog(shader));
        return shader;
    }

    public void Use() => _gl.UseProgram(Handle);

    private int Location(string name)
    {
        if (!_uniforms.TryGetValue(name, out var loc))
        {
            loc = _gl.GetUniformLocation(Handle, name);
            _uniforms[name] = loc;
        }
        return loc;
    }

    public unsafe void Set(string name, float[] matrix4)
    {
        fixed (float* p = matrix4) _gl.UniformMatrix4(Location(name), 1, false, p);
    }

    public void Set(string name, float value) => _gl.Uniform1(Location(name), value);
    public void Set(string name, int value) => _gl.Uniform1(Location(name), value);
    public void Set(string name, float x, float y) => _gl.Uniform2(Location(name), x, y);
    public void Set(string name, float x, float y, float z) => _gl.Uniform3(Location(name), x, y, z);
    public void Set(string name, float x, float y, float z, float w) => _gl.Uniform4(Location(name), x, y, z, w);

    public void Dispose() => _gl.DeleteProgram(Handle);
}
