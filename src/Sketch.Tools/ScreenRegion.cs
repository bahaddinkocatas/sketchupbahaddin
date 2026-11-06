using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Tools;

/// <summary>
/// A closed region in view pixels (a selection box or a lasso) and the window/crossing selection it makes:
/// window selects what is fully inside, crossing selects everything the region touches.
/// </summary>
public sealed class ScreenRegion
{
    private readonly List<Vector2d> _poly;

    public ScreenRegion(IEnumerable<Vector2d> polygon) => _poly = polygon.ToList();

    public static ScreenRegion Rectangle(double x0, double y0, double x1, double y1) =>
        new(new[] { new Vector2d(x0, y0), new Vector2d(x1, y0), new Vector2d(x1, y1), new Vector2d(x0, y1) });

    public IReadOnlyList<Vector2d> Points => _poly;

    public bool Contains(Vector2d p) => Polygon2d.Contains(_poly, p) || Polygon2d.IsOnBoundary(p, _poly, 1e-9);

    public bool Touches(Vector2d a, Vector2d b)
    {
        if (Contains(a) || Contains(b)) return true;
        for (int i = 0, n = _poly.Count; i < n; i++)
            if (SegmentsIntersect(a, b, _poly[i], _poly[(i + 1) % n])) return true;
        return false;
    }

    private static bool SegmentsIntersect(Vector2d p1, Vector2d p2, Vector2d q1, Vector2d q2)
    {
        static double Orient(Vector2d a, Vector2d b, Vector2d c) => Vector2d.Cross(b - a, c - a);
        var d1 = Orient(q1, q2, p1);
        var d2 = Orient(q1, q2, p2);
        var d3 = Orient(p1, p2, q1);
        var d4 = Orient(p1, p2, q2);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    /// <summary>Visible entities of the active context selected by this region.</summary>
    public List<Entity> Pick(IView view, bool crossing)
    {
        var cam = view.Camera;

        Vector2d? Screen(Vector3d p)
        {
            var s = cam.WorldToScreen(p, view.Width, view.Height);
            return s is null ? null : new Vector2d(s.Value.X, s.Value.Y);
        }

        bool EdgeTouches(Edge edge) =>
            Screen(edge.Start.Position) is { } a && Screen(edge.End.Position) is { } b && Touches(a, b);

        // An image is hit like a face: by its outline, or by a region lying entirely inside it.
        bool ImageTouches(ImageEntity image)
        {
            var poly = image.Corners.Select(Screen).ToList();
            if (poly.Any(p => p is null)) return false;
            var pts = poly.Select(p => p!.Value).ToList();
            return Enumerable.Range(0, 4).Any(i => Touches(pts[i], pts[(i + 1) % 4]))
                   || (_poly.Count > 0 && Polygon2d.Contains(pts, _poly[0]));
        }

        var result = new List<Entity>();
        var ents = view.Model.ActiveEntities;
        foreach (var g in ents.Groups)
        {
            if (!Entities.IsEffectivelyVisible(g)) continue;
            // Everything that gives the group a place on screen: geometry, images (e.g. a group holding only a
            // floor plan picture) and annotations.
            var inside = g.Entities;
            var anchors = inside.AllVertices().Select(v => v.Position)
                .Concat(inside.AllImages().SelectMany(i => i.Corners))
                .Concat(inside.AllNotes().Select(n => n.Anchor))
                .Concat(inside.AllDimensions().SelectMany(d => new[] { d.Start, d.End }))
                .Select(Screen).ToList();
            if (anchors.Count == 0 || anchors.Any(p => p is null)) continue;
            var hit = crossing
                ? inside.AllEdges().Any(EdgeTouches) || inside.AllImages().Any(ImageTouches) || anchors.Any(p => Contains(p!.Value))
                : anchors.All(p => Contains(p!.Value));
            if (hit) result.Add(g);
        }
        foreach (var e in ents.Edges)
        {
            if (!Entities.IsEffectivelyVisible(e)) continue;
            var a = Screen(e.Start.Position);
            var b = Screen(e.End.Position);
            if (a is null || b is null) continue;
            if (crossing ? Touches(a.Value, b.Value) : Contains(a.Value) && Contains(b.Value)) result.Add(e);
        }
        foreach (var f in ents.Faces)
        {
            if (!Entities.IsEffectivelyVisible(f)) continue;
            var pts = f.OuterLoop.Vertices.Select(v => Screen(v.Position)).ToList();
            if (pts.Any(p => p is null)) continue;
            var poly = pts.Select(p => p!.Value).ToList();
            bool hit;
            if (!crossing) hit = poly.All(Contains);
            else
            {
                // Touching an edge, or the region lying entirely inside the face.
                hit = Enumerable.Range(0, poly.Count).Any(i => Touches(poly[i], poly[(i + 1) % poly.Count]))
                      || (_poly.Count > 0 && Polygon2d.Contains(poly, _poly[0]));
            }
            if (hit) result.Add(f);
        }
        foreach (var x in ents.Guides.Cast<Entity>().Concat(ents.Dimensions).Concat(ents.Notes).Concat(ents.Images).Concat(ents.Sections))
        {
            if (!Entities.IsEffectivelyVisible(x)) continue;
            var anchors = AnchorsOf(x).Select(Screen).ToList();
            if (anchors.Count == 0 || anchors.Any(p => p is null)) continue;
            var hit = crossing
                ? anchors.Any(p => Contains(p!.Value)) || x is ImageEntity image && ImageTouches(image)
                : anchors.All(p => Contains(p!.Value));
            if (hit) result.Add(x);
        }
        return result;
    }

    private static IEnumerable<Vector3d> AnchorsOf(Entity e) => e switch
    {
        Guide g => new[] { g.Point },
        Dimension d => new[] { d.Start, d.End },
        Note n => new[] { n.Anchor },
        ImageEntity i => i.Corners,
        SectionPlane s => s.Corners,
        _ => Array.Empty<Vector3d>(),
    };
}
