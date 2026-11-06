namespace Sketch.Geometry;

/// <summary>
/// Double-precision 4x4 matrix using the column-vector convention (p' = M * p), stored row-major.
/// </summary>
public readonly struct Matrix4d
{
    private readonly double[] _m; // row-major, 16 entries

    private Matrix4d(double[] m) => _m = m;

    public double this[int row, int col] => (_m ?? IdentityArray)[row * 4 + col];

    private static readonly double[] IdentityArray = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };

    public static Matrix4d Identity => new((double[])IdentityArray.Clone());

    public static Matrix4d FromRows(params double[] rowMajor)
    {
        if (rowMajor.Length != 16) throw new ArgumentException("16 values required", nameof(rowMajor));
        return new Matrix4d((double[])rowMajor.Clone());
    }

    public static Matrix4d operator *(Matrix4d a, Matrix4d b)
    {
        var r = new double[16];
        for (var i = 0; i < 4; i++)
        for (var j = 0; j < 4; j++)
        {
            double s = 0;
            for (var k = 0; k < 4; k++) s += a[i, k] * b[k, j];
            r[i * 4 + j] = s;
        }
        return new Matrix4d(r);
    }

    public static Matrix4d Translation(Vector3d t) =>
        FromRows(1, 0, 0, t.X, 0, 1, 0, t.Y, 0, 0, 1, t.Z, 0, 0, 0, 1);

    public static Matrix4d Scaling(double sx, double sy, double sz) =>
        FromRows(sx, 0, 0, 0, 0, sy, 0, 0, 0, 0, sz, 0, 0, 0, 0, 1);

    /// <summary>Rotation around a unit axis through the origin.</summary>
    public static Matrix4d Rotation(Vector3d axis, double radians)
    {
        var a = axis.Normalized();
        double c = Math.Cos(radians), s = Math.Sin(radians), t = 1 - c;
        return FromRows(
            t * a.X * a.X + c, t * a.X * a.Y - s * a.Z, t * a.X * a.Z + s * a.Y, 0,
            t * a.X * a.Y + s * a.Z, t * a.Y * a.Y + c, t * a.Y * a.Z - s * a.X, 0,
            t * a.X * a.Z - s * a.Y, t * a.Y * a.Z + s * a.X, t * a.Z * a.Z + c, 0,
            0, 0, 0, 1);
    }

    /// <summary>Mirror about the plane through <paramref name="point"/> with the given normal.</summary>
    public static Matrix4d Reflection(Vector3d point, Vector3d normal)
    {
        var n = normal.Normalized();
        var m = FromRows(
            1 - 2 * n.X * n.X, -2 * n.X * n.Y, -2 * n.X * n.Z, 0,
            -2 * n.X * n.Y, 1 - 2 * n.Y * n.Y, -2 * n.Y * n.Z, 0,
            -2 * n.X * n.Z, -2 * n.Y * n.Z, 1 - 2 * n.Z * n.Z, 0,
            0, 0, 0, 1);
        return Translation(point) * m * Translation(-point);
    }

    /// <summary>Right-handed view matrix (camera looks down -Z in eye space).</summary>
    public static Matrix4d LookAt(Vector3d eye, Vector3d target, Vector3d up)
    {
        var f = (target - eye).Normalized();
        var s = Vector3d.Cross(f, up).Normalized();
        var u = Vector3d.Cross(s, f);
        return FromRows(
            s.X, s.Y, s.Z, -Vector3d.Dot(s, eye),
            u.X, u.Y, u.Z, -Vector3d.Dot(u, eye),
            -f.X, -f.Y, -f.Z, Vector3d.Dot(f, eye),
            0, 0, 0, 1);
    }

    /// <summary>OpenGL-style perspective projection (clip z in [-1, 1]).</summary>
    public static Matrix4d Perspective(double fovYRadians, double aspect, double near, double far)
    {
        var f = 1.0 / Math.Tan(fovYRadians / 2);
        return FromRows(
            f / aspect, 0, 0, 0,
            0, f, 0, 0,
            0, 0, (far + near) / (near - far), 2 * far * near / (near - far),
            0, 0, -1, 0);
    }

    /// <summary>OpenGL-style orthographic projection centred on the view axis.</summary>
    public static Matrix4d Orthographic(double width, double height, double near, double far) =>
        FromRows(
            2 / width, 0, 0, 0,
            0, 2 / height, 0, 0,
            0, 0, -2 / (far - near), -(far + near) / (far - near),
            0, 0, 0, 1);

    public Vector3d TransformPoint(Vector3d p)
    {
        var x = this[0, 0] * p.X + this[0, 1] * p.Y + this[0, 2] * p.Z + this[0, 3];
        var y = this[1, 0] * p.X + this[1, 1] * p.Y + this[1, 2] * p.Z + this[1, 3];
        var z = this[2, 0] * p.X + this[2, 1] * p.Y + this[2, 2] * p.Z + this[2, 3];
        var w = this[3, 0] * p.X + this[3, 1] * p.Y + this[3, 2] * p.Z + this[3, 3];
        return Math.Abs(w - 1) < 1e-15 || w == 0 ? new Vector3d(x, y, z) : new Vector3d(x / w, y / w, z / w);
    }

    /// <summary>Transforms a homogeneous point and returns (x, y, z, w) without dividing.</summary>
    public (double X, double Y, double Z, double W) TransformHomogeneous(Vector3d p) => (
        this[0, 0] * p.X + this[0, 1] * p.Y + this[0, 2] * p.Z + this[0, 3],
        this[1, 0] * p.X + this[1, 1] * p.Y + this[1, 2] * p.Z + this[1, 3],
        this[2, 0] * p.X + this[2, 1] * p.Y + this[2, 2] * p.Z + this[2, 3],
        this[3, 0] * p.X + this[3, 1] * p.Y + this[3, 2] * p.Z + this[3, 3]);

    public Vector3d TransformVector(Vector3d v) => new(
        this[0, 0] * v.X + this[0, 1] * v.Y + this[0, 2] * v.Z,
        this[1, 0] * v.X + this[1, 1] * v.Y + this[1, 2] * v.Z,
        this[2, 0] * v.X + this[2, 1] * v.Y + this[2, 2] * v.Z);

    public Matrix4d Inverse()
    {
        var m = _m ?? IdentityArray;
        var inv = new double[16];
        inv[0] = m[5] * m[10] * m[15] - m[5] * m[11] * m[14] - m[9] * m[6] * m[15] + m[9] * m[7] * m[14] + m[13] * m[6] * m[11] - m[13] * m[7] * m[10];
        inv[4] = -m[4] * m[10] * m[15] + m[4] * m[11] * m[14] + m[8] * m[6] * m[15] - m[8] * m[7] * m[14] - m[12] * m[6] * m[11] + m[12] * m[7] * m[10];
        inv[8] = m[4] * m[9] * m[15] - m[4] * m[11] * m[13] - m[8] * m[5] * m[15] + m[8] * m[7] * m[13] + m[12] * m[5] * m[11] - m[12] * m[7] * m[9];
        inv[12] = -m[4] * m[9] * m[14] + m[4] * m[10] * m[13] + m[8] * m[5] * m[14] - m[8] * m[6] * m[13] - m[12] * m[5] * m[10] + m[12] * m[6] * m[9];
        inv[1] = -m[1] * m[10] * m[15] + m[1] * m[11] * m[14] + m[9] * m[2] * m[15] - m[9] * m[3] * m[14] - m[13] * m[2] * m[11] + m[13] * m[3] * m[10];
        inv[5] = m[0] * m[10] * m[15] - m[0] * m[11] * m[14] - m[8] * m[2] * m[15] + m[8] * m[3] * m[14] + m[12] * m[2] * m[11] - m[12] * m[3] * m[10];
        inv[9] = -m[0] * m[9] * m[15] + m[0] * m[11] * m[13] + m[8] * m[1] * m[15] - m[8] * m[3] * m[13] - m[12] * m[1] * m[11] + m[12] * m[3] * m[9];
        inv[13] = m[0] * m[9] * m[14] - m[0] * m[10] * m[13] - m[8] * m[1] * m[14] + m[8] * m[2] * m[13] + m[12] * m[1] * m[10] - m[12] * m[2] * m[9];
        inv[2] = m[1] * m[6] * m[15] - m[1] * m[7] * m[14] - m[5] * m[2] * m[15] + m[5] * m[3] * m[14] + m[13] * m[2] * m[7] - m[13] * m[3] * m[6];
        inv[6] = -m[0] * m[6] * m[15] + m[0] * m[7] * m[14] + m[4] * m[2] * m[15] - m[4] * m[3] * m[14] - m[12] * m[2] * m[7] + m[12] * m[3] * m[6];
        inv[10] = m[0] * m[5] * m[15] - m[0] * m[7] * m[13] - m[4] * m[1] * m[15] + m[4] * m[3] * m[13] + m[12] * m[1] * m[7] - m[12] * m[3] * m[5];
        inv[14] = -m[0] * m[5] * m[14] + m[0] * m[6] * m[13] + m[4] * m[1] * m[14] - m[4] * m[2] * m[13] - m[12] * m[1] * m[6] + m[12] * m[2] * m[5];
        inv[3] = -m[1] * m[6] * m[11] + m[1] * m[7] * m[10] + m[5] * m[2] * m[11] - m[5] * m[3] * m[10] - m[9] * m[2] * m[7] + m[9] * m[3] * m[6];
        inv[7] = m[0] * m[6] * m[11] - m[0] * m[7] * m[10] - m[4] * m[2] * m[11] + m[4] * m[3] * m[10] + m[8] * m[2] * m[7] - m[8] * m[3] * m[6];
        inv[11] = -m[0] * m[5] * m[11] + m[0] * m[7] * m[9] + m[4] * m[1] * m[11] - m[4] * m[3] * m[9] - m[8] * m[1] * m[7] + m[8] * m[3] * m[5];
        inv[15] = m[0] * m[5] * m[10] - m[0] * m[6] * m[9] - m[4] * m[1] * m[10] + m[4] * m[2] * m[9] + m[8] * m[1] * m[6] - m[8] * m[2] * m[5];

        var det = m[0] * inv[0] + m[1] * inv[4] + m[2] * inv[8] + m[3] * inv[12];
        if (Math.Abs(det) < 1e-300) throw new InvalidOperationException("Matrix is singular.");
        for (var i = 0; i < 16; i++) inv[i] /= det;
        return new Matrix4d(inv);
    }

    /// <summary>Column-major float array for uploading to OpenGL.</summary>
    public float[] ToColumnMajorFloat()
    {
        var r = new float[16];
        for (var row = 0; row < 4; row++)
        for (var col = 0; col < 4; col++)
            r[col * 4 + row] = (float)this[row, col];
        return r;
    }
}
