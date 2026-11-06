using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Inference;

/// <summary>Everything the engine needs to know about one cursor position.</summary>
public sealed record InferenceQuery(Camera Camera, int Width, int Height, double X, double Y)
{
    /// <summary>Start point of the current operation (enables axis/parallel inferences from it).</summary>
    public Vector3d? From { get; init; }

    /// <summary>Face the operation started on; its plane is the default drawing plane.</summary>
    public Face? FromFace { get; init; }

    public InferenceLock? Lock { get; init; }

    /// <summary>Restricts point inferences to this plane (e.g. rectangle tool after the first corner).</summary>
    public Plane3d? DrawingPlane { get; init; }
}

/// <summary>
/// Snapping engine shared by all tools. Priority (highest first): endpoint, midpoint/intersection/origin,
/// line-from-point, axis line from the start point, from-point alignment, on edge, on face, free point.
/// </summary>
public sealed partial class InferenceEngine
{
    public const double PointRadiusPx = 9;
    public const double EdgeRadiusPx = 6;
    public const double LineRadiusPx = 9;

    private readonly Entities _entities;
    private readonly List<Vector3d> _references = new();

    public InferenceEngine(Entities entities)
    {
        _entities = entities;
        _entities.Changed += (_, _) => OnGeometryChanged();
    }

    // Everything visible in the model, including geometry inside groups (hidden entities and hidden tags excluded).
    private IReadOnlyList<Edge> VisibleEdges() => Model.Edges;
    private IReadOnlyList<Face> VisibleFaces() => Model.Faces;

    /// <summary>Recently hovered points; they produce dotted "from point" alignments.</summary>
    public IReadOnlyList<Vector3d> ReferencePoints => _references;

    public void ClearReferences() => _references.Clear();

    private void Remember(Vector3d p)
    {
        _references.RemoveAll(r => r.IsEqual(p));
        _references.Insert(0, p);
        if (_references.Count > 3) _references.RemoveAt(_references.Count - 1);
    }

    /// <summary>Current drawing axes (the host points this at the model's axes).</summary>
    public Func<ModelAxes> Frame { get; set; } = () => ModelAxes.World;

    public (Vector3d Dir, InferenceAxis Axis)[] Axes
    {
        get
        {
            var f = Frame();
            return new[] { (f.X, InferenceAxis.Red), (f.Y, InferenceAxis.Green), (f.Z, InferenceAxis.Blue) };
        }
    }

    public InferenceResult Infer(InferenceQuery q)
    {
        var result = InferCore(q);
        if (result.Face is not null) return result;

        // Every result that lies on the face under the cursor carries that face: tools use it to choose their
        // drawing plane (e.g. a rectangle started on an endpoint of a wall is drawn on the wall).
        var ray = q.Camera.RayThroughPixel(q.X, q.Y, q.Width, q.Height);
        var under = FaceInference(ray)?.Face;
        if (under is not null && Math.Abs(under.Plane.SignedDistance(result.Position)) <= Tolerance.Length * 10)
            return result with { Face = under };
        if (result.Edge is { } e)
        {
            // On an edge with no face under the cursor: prefer an adjacent face facing the camera.
            var facing = e.Faces.FirstOrDefault(f => Vector3d.Dot(f.Normal, ray.Direction) < 0) ?? e.Faces.FirstOrDefault();
            if (facing is not null) return result with { Face = facing };
        }
        return result;
    }

    private InferenceResult InferCore(InferenceQuery q)
    {
        var ray = q.Camera.RayThroughPixel(q.X, q.Y, q.Width, q.Height);
        var point = PointInference(q, ray);

        if (q.Lock is LineLock line)
        {
            // Locked to a line: a hovered point still sets the distance along it; crossing an edge snaps to it.
            if (point is null && EdgeCrossing(q, line.Origin, line.Direction, line.Axis) is { } crossing) return crossing;
            var on = point is not null
                ? ProjectOnLine(point.Position, line.Origin, line.Direction)
                : ClosestOnLineToRay(line.Origin, line.Direction, ray);
            return new InferenceResult(on, InferenceKind.OnLine)
            {
                LineOrigin = line.Origin, LineDirection = line.Direction, Axis = line.Axis,
                ReferencePoint = point?.Position,
            };
        }

        if (point is not null)
        {
            if (point.Kind == InferenceKind.Endpoint) Remember(point.Position);
            if (q.Lock is PlaneLock pl) return point with { Position = pl.Plane.Project(point.Position) };
            return point;
        }

        var fromLines = q.From is { } from ? LinesFrom(q, from) : new List<(Vector3d O, Vector3d D, InferenceAxis A)>();
        if (q.DrawingPlane is { } drawingPlane)
        {
            // On a fixed drawing plane only directions lying in that plane make sense.
            fromLines = fromLines.Where(l => Math.Abs(Vector3d.Dot(l.D, drawingPlane.Normal)) < 1e-6).ToList();
        }
        var refLines = ReferenceLines(q);

        // Intersection of an axis line from the start with a reference alignment line.
        InferenceResult? combined = null;
        var bestCombined = double.MaxValue;
        foreach (var l in fromLines)
        foreach (var r in refLines)
        {
            if (Segment3d.AreParallel(l.D, r.D)) continue;
            var p = ClosestBetweenLines(l.O, l.D, r.O, r.D, out var gap);
            if (p is null || gap > Tolerance.Length * 10) continue;
            var d = ScreenDistance(q, p.Value);
            if (d < LineRadiusPx && d < bestCombined)
            {
                bestCombined = d;
                combined = new InferenceResult(p.Value, InferenceKind.LineFromPoint)
                {
                    LineOrigin = l.O, LineDirection = l.D, Axis = l.A, ReferencePoint = r.O, ReferenceAxis = r.A,
                };
            }
        }
        if (combined is not null) return combined;

        // Axis / parallel line from the start point.
        InferenceResult? linear = null;
        var bestLinear = LineRadiusPx;
        foreach (var l in fromLines)
        {
            var d = ScreenDistanceToLine(q, l.O, l.D);
            if (d < bestLinear)
            {
                bestLinear = d;
                linear = new InferenceResult(ClosestOnLineToRay(l.O, l.D, ray), InferenceKind.OnLine)
                {
                    LineOrigin = l.O, LineDirection = l.D, Axis = l.A,
                };
            }
        }

        if (linear is not null)
        {
            // Where the axis line crosses an edge near the cursor, snap exactly onto that edge.
            return EdgeCrossing(q, linear.LineOrigin!.Value, linear.LineDirection!.Value, linear.Axis) ?? linear;
        }

        // An edge under the cursor wins over a dotted alignment; both together give their crossing.
        var edgeUnder = EdgeInference(q, ray);

        // Alignment with a reference point (no start point, or start point lines not hit). The alignment line is
        // projected onto the surface the point will lie on (the drawing plane, else the face under the cursor, else
        // the default plane), as in SketchUp: aligning with a corner at the back of a box still gives a point on
        // the visible face, never one at the depth of that corner.
        InferenceResult? fromPoint = null;
        var bestRef = LineRadiusPx;
        var faceUnder = q.DrawingPlane is null ? FaceInference(ray)?.Face : null;
        var surface = q.DrawingPlane ?? faceUnder?.Plane ?? DefaultPlane(q, ray);
        var alignOrigin = Vector3d.Zero;
        var alignDir = Vector3d.Zero;
        foreach (var r in refLines)
        {
            var dir = r.D - surface.Normal * Vector3d.Dot(r.D, surface.Normal);
            if (dir.IsZero(1e-6)) continue; // the alignment runs straight into the surface
            dir = dir.Normalized();
            var o = surface.Project(r.O);
            var d = ScreenDistanceToLine(q, o, dir);
            if (d >= bestRef) continue;
            var p = ClosestOnLineToRay(o, dir, ray);
            if (faceUnder is not null && !faceUnder.ContainsPoint(p, Tolerance.Length * 10)) continue;
            bestRef = d;
            alignOrigin = o;
            alignDir = dir;
            fromPoint = new InferenceResult(p, InferenceKind.FromPoint) { ReferencePoint = r.O, ReferenceAxis = r.A, Face = faceUnder };
        }
        if (fromPoint is not null && edgeUnder is null) return fromPoint;
        if (edgeUnder is not null)
        {
            if (fromPoint is not null && EdgeCrossing(q, alignOrigin, alignDir, InferenceAxis.None) is { } aligned)
                return aligned with { ReferencePoint = fromPoint.ReferencePoint, ReferenceAxis = fromPoint.ReferenceAxis, LineOrigin = null, LineDirection = null };
            return Constrain(q, edgeUnder);
        }

        var guideHit = GuideInference(q, ray);
        if (guideHit is not null) return Constrain(q, guideHit);

        var faceHit = FaceInference(ray);
        if (q.DrawingPlane is null && ImageHit(ray) is { } imageHit
            && (faceHit is null || imageHit.T < Vector3d.Dot(faceHit.Position - ray.Origin, ray.Direction)))
        {
            // Drawing on an imported plan: points lie on the image.
            return Constrain(q, new InferenceResult(ray.PointAt(imageHit.T), InferenceKind.OnImage) { Image = imageHit.Image });
        }
        if (faceHit is not null && q.DrawingPlane is null) return Constrain(q, faceHit);

        // Free point on the drawing plane.
        var t = (q.DrawingPlane ?? DefaultPlane(q, ray)).Intersect(ray);
        var free = t is > 0 ? ray.PointAt(t.Value) : ray.PointAt(q.Camera.Distance);
        return Constrain(q, new InferenceResult(free, InferenceKind.None) { Face = faceHit?.Face });
    }

    private static InferenceResult Constrain(InferenceQuery q, InferenceResult r) =>
        q.Lock is PlaneLock pl ? r with { Position = pl.Plane.Project(r.Position) } : r;

    /// <summary>Drawing plane when nothing is under the cursor: the start face's plane, else horizontal through the start, else the ground.</summary>
    private Plane3d DefaultPlane(InferenceQuery q, Ray3d ray)
    {
        if (q.FromFace is { IsDeleted: false } f) return f.Plane;
        var axes = Frame();
        return new Plane3d(q.From ?? axes.Origin, ViewPlaneNormal(q.Camera, axes));
    }

    /// <summary>
    /// Plane for drawing in empty space, chosen from the viewing angle as in SketchUp: the ground (red-green)
    /// plane unless the view is close to horizontal, then the vertical axis plane that faces the camera
    /// (red-blue from the front/back, green-blue from the sides).
    /// </summary>
    public static Vector3d ViewPlaneNormal(Camera camera, ModelAxes axes)
    {
        var d = camera.Direction;
        if (Math.Abs(Vector3d.Dot(d, axes.Z)) >= 0.35) return axes.Z;
        return Math.Abs(Vector3d.Dot(d, axes.Y)) >= Math.Abs(Vector3d.Dot(d, axes.X)) ? axes.Y : axes.X;
    }

    // ------------------------------------------------------------------ point inferences

    /// <summary>
    /// Most candidates checked for being hidden behind a face, nearest first. Occlusion is the expensive part, so
    /// it is only tested for the best candidates: a dense cluster (e.g. 100 000 points of small 3D text under the
    /// cursor) must not freeze the mouse.
    /// </summary>
    private const int MaxOcclusionTests = 48;

    private InferenceResult? PointInference(InferenceQuery q, Ray3d ray)
    {
        var candidates = new BestCandidates<(Vector3d P, InferenceKind Kind, Vertex? V, Edge? E)>();

        void Offer(Vector3d p, double sx, double sy, InferenceKind kind, double bias, Vertex? v, Edge? e)
        {
            var d = Math.Sqrt((sx - q.X) * (sx - q.X) + (sy - q.Y) * (sy - q.Y));
            if (!(d <= PointRadiusPx)) return; // also rejects NaN (behind the eye)
            if (q.DrawingPlane is { } dp && Math.Abs(dp.SignedDistance(p)) > Tolerance.Length) return;
            candidates.Offer(d + bias, (p, kind, v, e));
        }

        void Consider(Vector3d p, InferenceKind kind, double bias)
        {
            if (q.Camera.WorldToScreen(p, q.Width, q.Height) is { } s) Offer(p, s.X, s.Y, kind, bias, null, null);
        }

        // Vertices and midpoints use the screen positions already in the index (there can be 100 000+ of them).
        foreach (var (v, x, y) in VerticesNear(q)) Offer(v.Position, x, y, InferenceKind.Endpoint, 0, v, null);
        foreach (var (e, x, y) in MidpointEdgesNear(q))
            Offer((e.Start.Position + e.End.Position) / 2, x, y, InferenceKind.Midpoint, 2, null, e);
        foreach (var g in VisibleGuides())
            if (g.IsPoint) Consider(g.Point, InferenceKind.GuidePoint, 0.5);
        foreach (var img in VisibleImages())
            foreach (var c in img.Corners) Consider(c, InferenceKind.Endpoint, 0.5);
        foreach (var p in GuideIntersections(q)) Consider(p, InferenceKind.Intersection, 1);
        Consider(Frame().Origin, InferenceKind.Origin, 3);

        foreach (var c in candidates.Ordered())
            if (!IsOccluded(q, c.P)) return new InferenceResult(c.P, c.Kind) { Vertex = c.V, Edge = c.E };
        return null;
    }

    private IEnumerable<Guide> VisibleGuides() => _entities.AllGuides().Where(Entities.IsEffectivelyVisible);

    /// <summary>Where guide lines near the cursor cross each other or cross edges.</summary>
    private IEnumerable<Vector3d> GuideIntersections(InferenceQuery q)
    {
        var near = VisibleGuides().Where(g => !g.IsPoint && ScreenDistanceToLine(q, g.Point, g.Direction) < 20).ToList();
        if (near.Count == 0) yield break;
        var others = near.Select(g => (O: g.Point, D: g.Direction, Segment: false, B: Vector3d.Zero))
            .Concat(VisibleEdges().Select(e => (O: e.Start.Position, D: e.End.Position - e.Start.Position, Segment: true, B: e.End.Position)))
            .ToList();
        foreach (var g in near)
        {
            foreach (var o in others)
            {
                if (o.O.IsEqual(g.Point) && o.D.IsEqual(g.Direction)) continue;
                if (Segment3d.AreParallel(g.Direction, o.D)) continue;
                var p = ClosestBetweenLines(g.Point, g.Direction, o.O, o.D.Normalized(), out var gap);
                if (p is null || gap > Tolerance.Length * 10) continue;
                if (o.Segment)
                {
                    var (t, d) = Segment3d.ProjectPoint(p.Value, o.O, o.B);
                    if (t < -1e-9 || t > 1 + 1e-9 || d > Tolerance.Length * 10) continue;
                }
                yield return p.Value;
            }
        }
    }

    /// <summary>True when a face or an image (drawn opaque, e.g. a floor plan) lies between the eye and the point.</summary>
    private bool IsOccluded(InferenceQuery q, Vector3d p)
    {
        var s = q.Camera.WorldToScreen(p, q.Width, q.Height);
        if (s is null) return true;
        var ray = q.Camera.RayThroughPixel(s.Value.X, s.Value.Y, q.Width, q.Height);
        var target = Vector3d.Dot(p - ray.Origin, ray.Direction);
        // Relative margin: distance along the ray in perspective, the view height in parallel projection
        // (where rays start far behind the eye).
        var margin = Math.Max(Tolerance.Length * 10, (q.Camera.IsPerspective ? target : q.Camera.OrthoHeight) * 1e-4);
        foreach (var f in FacesAlongRay(ray, target))
        {
            var t = f.Plane.Intersect(ray);
            if (t is null || t.Value <= 0 || t.Value >= target - margin) continue;
            if (f.ContainsPoint(ray.PointAt(t.Value), Tolerance.Length)) return true;
        }
        foreach (var img in VisibleImages())
            if (img.Intersect(ray) is { } t && t < target - margin) return true;
        return false;
    }

    // ------------------------------------------------------------------ linear inferences

    private List<(Vector3d O, Vector3d D, InferenceAxis A)> LinesFrom(InferenceQuery q, Vector3d from)
    {
        var lines = Axes.Select(a => (from, a.Dir, a.Axis)).ToList();
        // Parallel / perpendicular to edges meeting at the start point.
        var start = Model.VertexAt(PosKey(from));
        if (start is not null)
        {
            foreach (var e in start.Edges)
            {
                var d = e.Direction;
                if (Axes.Any(a => Segment3d.AreParallel(a.Dir, d))) continue;
                lines.Add((from, d, InferenceAxis.Parallel));
                var perp = Vector3d.Cross(d, Frame().Z);
                if (!perp.IsZero(1e-9)) lines.Add((from, perp.Normalized(), InferenceAxis.Perpendicular));
            }
        }
        return lines;
    }

    private List<(Vector3d O, Vector3d D, InferenceAxis A)> ReferenceLines(InferenceQuery q)
    {
        var lines = new List<(Vector3d, Vector3d, InferenceAxis)>();
        foreach (var r in _references)
        {
            if (q.From is { } from && r.IsEqual(from)) continue;
            foreach (var (dir, axis) in Axes)
            {
                if (q.DrawingPlane is { } dp && Math.Abs(Vector3d.Dot(dir, dp.Normal)) > 1e-6) continue;
                lines.Add((r, dir, axis));
            }
        }
        return lines;
    }

    // ------------------------------------------------------------------ edges & faces

    private InferenceResult? GuideInference(InferenceQuery q, Ray3d ray)
    {
        InferenceResult? best = null;
        var bestD = EdgeRadiusPx;
        foreach (var g in VisibleGuides())
        {
            if (g.IsPoint) continue;
            var d = ScreenDistanceToLine(q, g.Point, g.Direction);
            if (d >= bestD) continue;
            var p = ClosestOnLineToRay(g.Point, g.Direction, ray);
            if (q.DrawingPlane is { } dp && Math.Abs(dp.SignedDistance(p)) > Tolerance.Length) continue;
            bestD = d;
            best = new InferenceResult(p, InferenceKind.OnGuide) { Guide = g };
        }
        return best;
    }

    /// <summary>Visible guide (line or point) within the pick radius of the cursor, or null.</summary>
    public Guide? PickGuide(InferenceQuery q)
    {
        Guide? best = null;
        var bestD = EdgeRadiusPx;
        foreach (var g in VisibleGuides())
        {
            var d = g.IsPoint ? ScreenDistance(q, g.Point) : ScreenDistanceToLine(q, g.Point, g.Direction);
            if (d >= bestD) continue;
            bestD = d;
            best = g;
        }
        return best;
    }

    /// <summary>Visible edge within the pick radius of the cursor, or null.</summary>
    public Edge? PickEdge(InferenceQuery q)
    {
        var ray = q.Camera.RayThroughPixel(q.X, q.Y, q.Width, q.Height);
        return EdgeInference(q with { DrawingPlane = null }, ray)?.Edge;
    }

    /// <summary>Entity under the cursor for selection: edges win when close enough, else the nearest face.</summary>
    /// <summary>Dimension or note under the cursor (its line or label), or null.</summary>
    public Entity? PickAnnotation(InferenceQuery q)
    {
        Entity? best = null;
        var bestD = 8.0;
        Vector2d? S(Vector3d p) => q.Camera.WorldToScreen(p, q.Width, q.Height) is { } s ? new Vector2d(s.X, s.Y) : null;
        var cursor = new Vector2d(q.X, q.Y);
        foreach (var d in _entities.AllDimensions().Where(Entities.IsEffectivelyVisible))
        {
            if (S(d.Start + d.Offset) is not { } a || S(d.End + d.Offset) is not { } b) continue;
            var dist = Math.Min(Polygon2d.DistanceToSegment(cursor, a, b), ((a + b) * 0.5 - cursor).Length - 10);
            if (dist < bestD)
            {
                bestD = dist;
                best = d;
            }
        }
        foreach (var sp in _entities.AllSections().Where(Entities.IsEffectivelyVisible))
        {
            var c = sp.Corners.Select(S).ToList();
            if (c.Any(p => p is null)) continue;
            for (var i = 0; i < 4; i++)
            {
                var dist = Polygon2d.DistanceToSegment(cursor, c[i]!.Value, c[(i + 1) % 4]!.Value) - 2;
                if (dist < bestD)
                {
                    bestD = dist;
                    best = sp;
                }
            }
        }
        foreach (var n in _entities.AllNotes().Where(Entities.IsEffectivelyVisible))
        {
            if (S(n.LabelPosition) is not { } l) continue;
            var dist = (l - cursor).Length - 14;
            if (n.HasLeader && S(n.Anchor) is { } a) dist = Math.Min(dist, Polygon2d.DistanceToSegment(cursor, a, l));
            if (dist < bestD)
            {
                bestD = dist;
                best = n;
            }
        }
        return best;
    }

    public Entity? PickEntity(InferenceQuery q)
    {
        var annotation = PickAnnotation(q);
        if (annotation is not null) return annotation;
        var edge = PickEdge(q);
        if (edge is not null) return edge;
        var guide = PickGuide(q);
        if (guide is not null) return guide;
        var ray = q.Camera.RayThroughPixel(q.X, q.Y, q.Width, q.Height);
        var face = FaceInference(ray);
        var image = ImageHit(ray);
        if (image is { } im && (face is null || im.T < Vector3d.Dot(face.Position - ray.Origin, ray.Direction) - Tolerance.Length))
            return im.Image;
        return face?.Face;
    }

    private InferenceResult? EdgeInference(InferenceQuery q, Ray3d ray)
    {
        var candidates = new BestCandidates<(Edge E, Vector3d P)>();
        foreach (var (e, a, b) in EdgesNear(q, q.X, q.Y))
        {
            if (e.Hidden) continue;
            var d = Polygon2d.DistanceToSegment(new Vector2d(q.X, q.Y), a, b);
            if (d >= EdgeRadiusPx) continue;
            var p = ClosestOnSegmentToRay(e.Start.Position, e.End.Position, ray);
            if (q.DrawingPlane is { } dp && Math.Abs(dp.SignedDistance(p)) > Tolerance.Length) continue;
            candidates.Offer(d, (e, p));
        }
        // Nearest first; only the best few are tested for being hidden (see MaxOcclusionTests).
        foreach (var (e, p) in candidates.Ordered())
            if (!IsOccluded(q, p)) return new InferenceResult(p, InferenceKind.OnEdge) { Edge = e };
        return null;
    }

    /// <summary>Point on the nearest visible surface (face or image) along the ray, or null.</summary>
    public Vector3d? SurfaceHit(Ray3d ray)
    {
        var face = FaceInference(ray);
        var image = ImageHit(ray);
        if (image is { } im && (face is null || im.T < Vector3d.Dot(face.Position - ray.Origin, ray.Direction)))
            return ray.PointAt(im.T);
        return face?.Position;
    }

    private IEnumerable<ImageEntity> VisibleImages() => _entities.AllImages().Where(Entities.IsEffectivelyVisible);

    /// <summary>Nearest visible image under the ray, with the ray parameter.</summary>
    public (ImageEntity Image, double T)? ImageHit(Ray3d ray)
    {
        (ImageEntity, double)? best = null;
        foreach (var img in VisibleImages())
        {
            if (img.Intersect(ray) is not { } t) continue;
            if (best is null || t < best.Value.Item2) best = (img, t);
        }
        return best;
    }

    /// <summary>Nearest face under the ray.</summary>
    public InferenceResult? FaceInference(Ray3d ray)
    {
        Face? bestFace = null;
        var bestT = double.MaxValue;
        foreach (var f in FacesAlongRay(ray))
        {
            var t = f.Plane.Intersect(ray);
            if (t is null || t.Value <= 0 || t.Value >= bestT) continue;
            if (!f.ContainsPoint(ray.PointAt(t.Value))) continue;
            bestT = t.Value;
            bestFace = f;
        }
        return bestFace is null ? null : new InferenceResult(ray.PointAt(bestT), InferenceKind.OnFace) { Face = bestFace };
    }

    /// <summary>
    /// Point where a construction line (axis from the start point, a lock, an alignment) crosses a visible edge
    /// close to the cursor, as an "on edge" inference that keeps the line's axis. Null when there is none.
    /// </summary>
    private InferenceResult? EdgeCrossing(InferenceQuery q, Vector3d origin, Vector3d dir, InferenceAxis axis)
    {
        if (dir.IsZero(1e-12)) return null;
        var cursor = new Vector2d(q.X, q.Y);
        InferenceResult? best = null;
        var bestD = LineRadiusPx + 3;
        foreach (var (e, sa, sb) in EdgesNear(q, q.X, q.Y))
        {
            if (e.Hidden) continue;
            if (Polygon2d.DistanceToSegment(cursor, sa, sb) > bestD) continue;
            var a = e.Start.Position;
            var ed = e.End.Position - a;
            var len = ed.Length;
            if (len < Tolerance.Length) continue;
            var u = ed / len;
            if (Segment3d.AreParallel(u, dir)) continue;
            if (ClosestBetweenLines(origin, dir, a, u, out var gap) is null || gap > Tolerance.Length * 50) continue;
            // The exact point on the construction line.
            var w = origin - a;
            var bb = Vector3d.Dot(dir, u);
            var denom = Vector3d.Dot(dir, dir) - bb * bb;
            var sParam = (bb * Vector3d.Dot(u, w) - Vector3d.Dot(dir, w)) / denom;
            var p = origin + dir * sParam;
            var t = Vector3d.Dot(p - a, u);
            if (t < -Tolerance.Length || t > len + Tolerance.Length) continue;
            if (q.DrawingPlane is { } dp && Math.Abs(dp.SignedDistance(p)) > Tolerance.Length * 10) continue;
            var d = ScreenDistance(q, p);
            if (d >= bestD) continue;
            bestD = d;
            best = new InferenceResult(p, InferenceKind.OnEdge) { Edge = e, LineOrigin = origin, LineDirection = dir, Axis = axis };
        }
        return best;
    }

    // ------------------------------------------------------------------ math helpers

    private static double ScreenDistance(InferenceQuery q, Vector3d p)
    {
        var s = q.Camera.WorldToScreen(p, q.Width, q.Height);
        if (s is null) return double.MaxValue;
        return Math.Sqrt((s.Value.X - q.X) * (s.Value.X - q.X) + (s.Value.Y - q.Y) * (s.Value.Y - q.Y));
    }

    /// <summary>Pixel distance from the cursor to the screen projection of an infinite 3D line.</summary>
    private static double ScreenDistanceToLine(InferenceQuery q, Vector3d origin, Vector3d dir)
    {
        var ray = q.Camera.RayThroughPixel(q.X, q.Y, q.Width, q.Height);
        var p = ClosestOnLineToRay(origin, dir, ray);
        return ScreenDistance(q, p);
    }

    public static Vector3d ProjectOnLine(Vector3d p, Vector3d origin, Vector3d dir) =>
        origin + dir * Vector3d.Dot(p - origin, dir);

    /// <summary>Point on the infinite line closest to the ray.</summary>
    public static Vector3d ClosestOnLineToRay(Vector3d origin, Vector3d dir, Ray3d ray)
    {
        var w0 = origin - ray.Origin;
        var a = Vector3d.Dot(dir, dir);
        var b = Vector3d.Dot(dir, ray.Direction);
        var c = Vector3d.Dot(ray.Direction, ray.Direction);
        var d = Vector3d.Dot(dir, w0);
        var e = Vector3d.Dot(ray.Direction, w0);
        var denom = a * c - b * b;
        if (Math.Abs(denom) < 1e-12) return origin;
        var s = (b * e - c * d) / denom;
        return origin + dir * s;
    }

    private static Vector3d ClosestOnSegmentToRay(Vector3d a, Vector3d b, Ray3d ray)
    {
        var dir = b - a;
        var len = dir.Length;
        if (len < 1e-12) return a;
        var p = ClosestOnLineToRay(a, dir / len, ray);
        var t = Math.Clamp(Vector3d.Dot(p - a, dir) / (len * len), 0, 1);
        return a + dir * t;
    }

    /// <summary>Midpoint of the shortest segment between two lines (null when parallel).</summary>
    private static Vector3d? ClosestBetweenLines(Vector3d o1, Vector3d d1, Vector3d o2, Vector3d d2, out double gap)
    {
        var w0 = o1 - o2;
        var a = Vector3d.Dot(d1, d1);
        var b = Vector3d.Dot(d1, d2);
        var c = Vector3d.Dot(d2, d2);
        var d = Vector3d.Dot(d1, w0);
        var e = Vector3d.Dot(d2, w0);
        var denom = a * c - b * b;
        gap = double.MaxValue;
        if (Math.Abs(denom) < 1e-12) return null;
        var s = (b * e - c * d) / denom;
        var t = (a * e - b * d) / denom;
        var p1 = o1 + d1 * s;
        var p2 = o2 + d2 * t;
        gap = (p1 - p2).Length;
        return (p1 + p2) / 2;
    }
}
