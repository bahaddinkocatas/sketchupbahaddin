using LibTessDotNet;
using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Rendering;

/// <summary>Triangulates planar faces with holes (via LibTessDotNet) in the face's own plane coordinates.</summary>
public static class FaceTriangulator
{
    /// <summary>Geometry of a face: its loops' points (loops separated by NaN) and its normal.</summary>
    private sealed class Key : IEquatable<Key>
    {
        private readonly double[] _values;
        private readonly int _hash;

        public Key(Face face)
        {
            var list = new List<double>(face.Vertices.Count() * 3 + 8) { face.Normal.X, face.Normal.Y, face.Normal.Z };
            var hash = new HashCode();
            foreach (var loop in face.Loops)
            {
                foreach (var v in loop.Vertices)
                {
                    var p = v.Position;
                    list.Add(p.X);
                    list.Add(p.Y);
                    list.Add(p.Z);
                    hash.Add(p.X);
                    hash.Add(p.Y);
                    hash.Add(p.Z);
                }
                list.Add(double.NaN);
            }
            _values = list.ToArray();
            _hash = hash.ToHashCode();
        }

        public bool Equals(Key? other) =>
            other is not null && other._hash == _hash && other._values.AsSpan().SequenceEqual(_values);

        public override bool Equals(object? obj) => Equals(obj as Key);
        public override int GetHashCode() => _hash;
    }

    private static readonly Dictionary<Key, List<Vector3d>> Cache = new();
    private const int CacheLimit = 200_000;

    /// <summary>Triangles of a face (cached: unchanged faces are not tessellated again after an edit).</summary>
    public static List<Vector3d> Triangulate(Face face)
    {
        var key = new Key(face);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        var result = Tessellate(face);
        if (Cache.Count >= CacheLimit) Cache.Clear();
        Cache[key] = result;
        return result;
    }

    private static List<Vector3d> Tessellate(Face face)
    {
        var origin = face.OuterLoop.Vertices[0].Position;
        var basis = new PlaneBasis(origin, face.Normal);
        var tess = new Tess();
        foreach (var loop in face.Loops)
        {
            var contour = loop.Vertices.Select(v =>
            {
                var p = basis.ToLocal(v.Position);
                return new ContourVertex(new Vec3((float)p.X, (float)p.Y, 0));
            }).ToArray();
            tess.AddContour(contour, ContourOrientation.Original);
        }
        tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);

        var result = new List<Vector3d>(tess.ElementCount * 3);
        for (var i = 0; i < tess.ElementCount; i++)
        {
            // Emit counter-clockwise around the face normal.
            var a = tess.Vertices[tess.Elements[i * 3]].Position;
            var b = tess.Vertices[tess.Elements[i * 3 + 1]].Position;
            var c = tess.Vertices[tess.Elements[i * 3 + 2]].Position;
            var pa = basis.ToWorld(new Vector2d(a.X, a.Y));
            var pb = basis.ToWorld(new Vector2d(b.X, b.Y));
            var pc = basis.ToWorld(new Vector2d(c.X, c.Y));
            var ccw = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X) >= 0;
            result.Add(pa);
            if (ccw)
            {
                result.Add(pb);
                result.Add(pc);
            }
            else
            {
                result.Add(pc);
                result.Add(pb);
            }
        }
        return result;
    }
}
