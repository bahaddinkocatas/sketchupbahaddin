using Sketch.Geometry;

namespace Sketch.Model;

public abstract class Entity
{
    public int Id { get; internal set; }
    public bool IsDeleted { get; internal set; }

    /// <summary>Tag of the entity; null means "Untagged".</summary>
    public Tag? Tag { get => _tag; set { _tag = value; Parent?.BumpVersion(); } }
    private Tag? _tag;

    /// <summary>Individually hidden (Edit &gt; Hide).</summary>
    public bool Hidden { get => _hidden; set { _hidden = value; Parent?.BumpVersion(); } }
    private bool _hidden;

    /// <summary>The container this entity lives in.</summary>
    public Entities? Parent { get; internal set; }
}

/// <summary>
/// Construction geometry from the Tape Measure / Protractor: an infinite guide line (non-zero direction) or a
/// guide point (zero direction). Guides are drawn dashed, snap like edges, and never form faces.
/// </summary>
public sealed class Guide : Entity
{
    public Vector3d Point { get; internal set; }
    public Vector3d Direction { get; internal set; }
    public bool IsPoint => Direction.IsZero(1e-12);

    public override string ToString() => IsPoint ? $"Guide Point {Point}" : $"Guide {Point}→{Direction}";
}

/// <summary>Linear dimension between two points, drawn offset from them; its text is the measured length.</summary>
public sealed class Dimension : Entity
{
    public Vector3d Start { get; internal set; }
    public Vector3d End { get; internal set; }

    /// <summary>Offset of the dimension line from the measured points.</summary>
    public Vector3d Offset { get; internal set; }

    public double Length => Vector3d.Distance(Start, End);
}

/// <summary>Text label, optionally with a leader line pointing at <see cref="Anchor"/>.</summary>
public sealed class Note : Entity
{
    public Vector3d Anchor { get; internal set; }

    /// <summary>Where the label sits, relative to the anchor.</summary>
    public Vector3d LabelOffset { get; internal set; }

    public string Text { get; set; } = string.Empty;
    public bool HasLeader { get; internal set; }

    public Vector3d LabelPosition => Anchor + LabelOffset;
}

/// <summary>
/// An imported raster image (e.g. a floor plan PNG) placed as a flat rectangle: corners at Origin, Origin+U,
/// Origin+U+V and Origin+V. The pixels live in the model's image store under <see cref="ImageKey"/>.
/// </summary>
public sealed class ImageEntity : Entity
{
    public Vector3d Origin { get; internal set; }
    public Vector3d U { get; internal set; }
    public Vector3d V { get; internal set; }
    public string ImageKey { get; internal set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Vector3d Normal => Vector3d.Cross(U, V).Normalized();
    public IReadOnlyList<Vector3d> Corners => new[] { Origin, Origin + U, Origin + U + V, Origin + V };

    /// <summary>Ray parameter where the ray hits the image rectangle, or null.</summary>
    public double? Intersect(Ray3d ray)
    {
        var t = new Plane3d(Origin, Normal).Intersect(ray);
        if (t is not > 0) return null;
        var d = ray.PointAt(t.Value) - Origin;
        var s = Vector3d.Dot(d, U) / U.LengthSquared;
        var r = Vector3d.Dot(d, V) / V.LengthSquared;
        return s is >= 0 and <= 1 && r is >= 0 and <= 1 ? t : null;
    }
}

/// <summary>
/// Section plane: while active it hides everything on the side opposite its <see cref="Normal"/> (the arrows
/// point at what stays visible) and outlines the cut. Drawn as a square of side 2·<see cref="Size"/>.
/// </summary>
public sealed class SectionPlane : Entity
{
    public Vector3d Point { get; internal set; }
    public Vector3d Normal { get; internal set; }
    public double Size { get; internal set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public Plane3d Plane => new(Point, Normal);

    public IReadOnlyList<Vector3d> Corners
    {
        get
        {
            var u = Normal.AnyPerpendicular();
            // Prefer a horizontal/vertical frame so the square looks tidy.
            if (Math.Abs(Normal.Z) < 0.9) u = Vector3d.Cross(Vector3d.UnitZ, Normal).Normalized();
            var v = Vector3d.Cross(Normal, u).Normalized();
            return new[] { Point - u * Size - v * Size, Point + u * Size - v * Size, Point + u * Size + v * Size, Point - u * Size + v * Size };
        }
    }

    public override string ToString() => string.IsNullOrEmpty(Name) ? $"Section Plane {Id}" : Name;
}

/// <summary>A named group: its own geometry container, so its geometry does not stick to geometry outside.</summary>
public sealed class Group : Entity
{
    internal Group(Entities content) => Entities = content;

    public string Name { get; set; } = string.Empty;

    /// <summary>Material for faces inside the group that have no material of their own.</summary>
    public Material? Material { get => _material; set { _material = value; Parent?.BumpVersion(); } }
    private Material? _material;

    public Entities Entities { get; }

    public override string ToString() => string.IsNullOrEmpty(Name) ? $"Group {Id}" : Name;
}

public sealed class Vertex : Entity
{
    internal readonly List<Edge> EdgeList = new();

    public Vector3d Position { get; internal set; }
    public IReadOnlyList<Edge> Edges => EdgeList;

    public override string ToString() => $"V{Id}{Position}";
}

public sealed class Edge : Entity
{
    internal readonly List<Face> FaceList = new();

    public Vertex Start { get; internal set; } = null!;
    public Vertex End { get; internal set; } = null!;
    public IReadOnlyList<Face> Faces => FaceList;

    /// <summary>Soft edges are hidden and smooth shading may flow across them.</summary>
    public bool Soft { get => _soft; set { _soft = value; Parent?.BumpVersion(); } }
    private bool _soft;

    public bool Smooth { get => _smooth; set { _smooth = value; Parent?.BumpVersion(); } }
    private bool _smooth;

    public double Length => Vector3d.Distance(Start.Position, End.Position);
    public Vector3d Direction => (End.Position - Start.Position).Normalized();

    public bool Uses(Vertex v) => ReferenceEquals(Start, v) || ReferenceEquals(End, v);

    public Vertex Other(Vertex v) => ReferenceEquals(v, Start) ? End : Start;

    public override string ToString() => $"E{Id}(V{Start.Id}-V{End.Id})";
}

/// <summary>Closed chain of vertices. Outer loops run counter-clockwise around the face normal, inner loops clockwise.</summary>
public sealed class Loop
{
    internal readonly List<Vertex> List;

    internal Loop(IEnumerable<Vertex> vertices) => List = vertices.ToList();

    public IReadOnlyList<Vertex> Vertices => List;

    public IEnumerable<(Vertex A, Vertex B)> Segments()
    {
        for (var i = 0; i < List.Count; i++)
            yield return (List[i], List[(i + 1) % List.Count]);
    }

    /// <summary>True when the loop walks from a to b (in this direction) somewhere.</summary>
    public bool Traverses(Vertex a, Vertex b)
    {
        for (var i = 0; i < List.Count; i++)
            if (ReferenceEquals(List[i], a) && ReferenceEquals(List[(i + 1) % List.Count], b))
                return true;
        return false;
    }

    public IReadOnlyList<Vector3d> Points => List.Select(v => v.Position).ToList();
}

public sealed class Face : Entity
{
    internal readonly List<Loop> InnerList = new();
    internal readonly List<Edge> EdgeList = new();

    public Loop OuterLoop { get; internal set; } = null!;
    public IReadOnlyList<Loop> InnerLoops => InnerList;
    public IEnumerable<Loop> Loops => InnerList.Prepend(OuterLoop);
    public IReadOnlyList<Edge> Edges => EdgeList;

    /// <summary>Unit normal of the front side.</summary>
    public Vector3d Normal { get; internal set; }

    /// <summary>Front-side material (null = default colour, or the enclosing group's material).</summary>
    public Material? Material { get => _material; set { _material = value; Parent?.BumpVersion(); } }
    private Material? _material;

    public Plane3d Plane => new(OuterLoop.List[0].Position, Normal);

    public IEnumerable<Vertex> Vertices => Loops.SelectMany(l => l.List);

    public double Area
    {
        get
        {
            var basis = new PlaneBasis(OuterLoop.List[0].Position, Normal);
            double area = 0;
            foreach (var loop in Loops)
                area += Polygon2d.SignedArea(loop.List.Select(v => basis.ToLocal(v.Position)).ToList());
            return area;
        }
    }

    /// <summary>True when the point lies inside the face (on its plane, inside the outer loop, outside the holes).</summary>
    public bool ContainsPoint(Vector3d p, double tolerance = Tolerance.Length)
    {
        if (Math.Abs(Plane.SignedDistance(p)) > tolerance) return false;
        var basis = new PlaneBasis(OuterLoop.List[0].Position, Normal);
        var q = basis.ToLocal(p);
        var outer = OuterLoop.List.Select(v => basis.ToLocal(v.Position)).ToList();
        if (!Polygon2d.Contains(outer, q) && !Polygon2d.IsOnBoundary(q, outer, tolerance)) return false;
        foreach (var hole in InnerList)
        {
            var h = hole.List.Select(v => basis.ToLocal(v.Position)).ToList();
            if (Polygon2d.ContainsStrict(h, q, tolerance)) return false;
        }
        return true;
    }

    public override string ToString() => $"F{Id}[{string.Join(",", OuterLoop.List.Select(v => v.Id))}]";
}
