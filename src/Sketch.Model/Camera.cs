using Sketch.Geometry;

namespace Sketch.Model;

/// <summary>
/// Model camera. World is Z-up (red = X, green = Y, blue = Z). The camera keeps a "gravity" up vector
/// so vertical edges stay vertical while orbiting, like SketchUp.
/// </summary>
public sealed class Camera
{
    public const double DefaultFovYDegrees = 35.0;

    public Vector3d Eye { get; private set; }
    public Vector3d Target { get; private set; }

    /// <summary>Up vector used to build the view (perpendicular to the view direction).</summary>
    public Vector3d Up { get; private set; }

    public bool IsPerspective { get; set; } = true;

    /// <summary>Vertical field of view in degrees (perspective only).</summary>
    public double FovYDegrees { get; set; } = DefaultFovYDegrees;

    /// <summary>How far behind the eye parallel-projection picking rays start (model units).</summary>
    public const double ParallelRayBackOffset = 1e6;

    /// <summary>Visible height in model units for parallel projection.</summary>
    public double OrthoHeight { get; set; } = 400;

    public Camera()
    {
        // Like SketchUp's default view: red runs to the lower right, green to the upper right, from above.
        var target = new Vector3d(40, 40, 20);
        Set(target + new Vector3d(1, -1, 0.72).Normalized() * 700, target, Vector3d.UnitZ);
    }

    public Vector3d Direction => (Target - Eye).Normalized();
    public Vector3d Right => Vector3d.Cross(Direction, Up).Normalized();
    public double Distance => Vector3d.Distance(Eye, Target);

    public void Set(Vector3d eye, Vector3d target, Vector3d upHint)
    {
        var dir = (target - eye).Normalized();
        if (dir.IsZero(1e-12)) throw new ArgumentException("Eye and target coincide.");
        var right = Vector3d.Cross(dir, upHint);
        if (right.IsZero(1e-9)) right = Vector3d.Cross(dir, Vector3d.UnitY);
        Eye = eye;
        Target = target;
        Up = Vector3d.Cross(right.Normalized(), dir).Normalized();
    }

    public Camera Clone()
    {
        var c = new Camera { IsPerspective = IsPerspective, FovYDegrees = FovYDegrees, OrthoHeight = OrthoHeight };
        c.Eye = Eye;
        c.Target = Target;
        c.Up = Up;
        return c;
    }

    public void CopyFrom(Camera other)
    {
        Eye = other.Eye;
        Target = other.Target;
        Up = other.Up;
        IsPerspective = other.IsPerspective;
        FovYDegrees = other.FovYDegrees;
        OrthoHeight = other.OrthoHeight;
    }

    public Matrix4d ViewMatrix => Matrix4d.LookAt(Eye, Target, Up);

    public Matrix4d ProjectionMatrix(double aspect, double near, double far) =>
        IsPerspective
            ? Matrix4d.Perspective(FovYDegrees * Math.PI / 180, aspect, near, far)
            : Matrix4d.Orthographic(OrthoHeight * aspect, OrthoHeight, near, far);

    /// <summary>Near/far planes chosen from the camera distance and the scene's bounding radius.</summary>
    public (double Near, double Far) ClipRange(Vector3d sceneCenter, double sceneRadius)
    {
        var toCenter = Vector3d.Dot(sceneCenter - Eye, Direction);
        var far = Math.Max(toCenter + sceneRadius * 1.5, Distance * 4);
        far = Math.Max(far, 100);
        if (!IsPerspective)
        {
            // Parallel views can see geometry behind the eye position; keep a symmetric range.
            var range = Math.Max(far, sceneRadius * 4 + Distance);
            return (-range, range);
        }
        var near = Math.Max(Math.Min(Distance, Math.Max(toCenter - sceneRadius, 0)) * 0.5, far / 20000);
        return (Math.Max(near, 0.01), far);
    }

    /// <summary>World-space ray through a pixel (origin top-left, y down).</summary>
    public Ray3d RayThroughPixel(double px, double py, double width, double height)
    {
        var ndcX = 2 * px / width - 1;
        var ndcY = 1 - 2 * py / height;
        var aspect = width / height;
        if (IsPerspective)
        {
            var tan = Math.Tan(FovYDegrees * Math.PI / 360);
            var dir = Direction + Right * (ndcX * tan * aspect) + Up * (ndcY * tan);
            return new Ray3d(Eye, dir.Normalized());
        }
        // A parallel view shows geometry on both sides of the eye position (e.g. a large model drawn before
        // zooming out), so picking rays start far behind it; otherwise the front of such a model is missed and
        // the face behind it is picked instead.
        var origin = Eye - Direction * ParallelRayBackOffset
                     + Right * (ndcX * OrthoHeight * aspect / 2) + Up * (ndcY * OrthoHeight / 2);
        return new Ray3d(origin, Direction);
    }

    /// <summary>
    /// Projects a world point to pixel coordinates (origin top-left). Returns null when the point is behind the
    /// eye (perspective). Depth is the distance along the view direction.
    /// </summary>
    public (double X, double Y, double Depth)? WorldToScreen(Vector3d p, double width, double height)
    {
        var rel = p - Eye;
        var depth = Vector3d.Dot(rel, Direction);
        var right = Vector3d.Dot(rel, Right);
        var up = Vector3d.Dot(rel, Up);
        double ndcX, ndcY;
        if (IsPerspective)
        {
            if (depth <= 1e-9) return null;
            var tan = Math.Tan(FovYDegrees * Math.PI / 360);
            ndcX = right / (depth * tan * (width / height));
            ndcY = up / (depth * tan);
        }
        else
        {
            ndcX = right / (OrthoHeight * width / height / 2);
            ndcY = up / (OrthoHeight / 2);
        }
        return ((ndcX + 1) / 2 * width, (1 - ndcY) / 2 * height, depth);
    }

    /// <summary>Model units per pixel at the given point (for screen-constant sizes).</summary>
    public double UnitsPerPixel(Vector3d atPoint, double viewportHeight)
    {
        if (!IsPerspective) return OrthoHeight / viewportHeight;
        var depth = Math.Max(Vector3d.Dot(atPoint - Eye, Direction), 1e-6);
        return 2 * depth * Math.Tan(FovYDegrees * Math.PI / 360) / viewportHeight;
    }

    // ---- Navigation primitives (used by Orbit/Pan/Zoom tools) ----

    /// <summary>
    /// Turntable orbit around a pivot: yaw about world Z, then change the view elevation by
    /// <paramref name="pitchRadians"/> (positive = look more upward, i.e. the eye moves down).
    /// </summary>
    public void Orbit(Vector3d pivot, double yawRadians, double pitchRadians)
    {
        var eye = Eye;
        var target = Target;

        // Yaw around world Z through pivot.
        eye = pivot + (eye - pivot).RotateAround(Vector3d.UnitZ, yawRadians);
        target = pivot + (target - pivot).RotateAround(Vector3d.UnitZ, yawRadians);
        var right = Right.RotateAround(Vector3d.UnitZ, yawRadians);

        // Pitch around the right axis, clamped so the view never flips over the pole.
        var dir = (target - eye).Normalized();
        var currentElevation = Math.Asin(Math.Clamp(dir.Z, -1, 1));
        const double limit = 89.5 * Math.PI / 180;
        var newElevation = Math.Clamp(currentElevation + pitchRadians, -limit, limit);
        // Rotating the eye offset about right = dir x up by +a raises the view direction's elevation by a.
        var applied = newElevation - currentElevation;
        eye = pivot + (eye - pivot).RotateAround(right, applied);
        target = pivot + (target - pivot).RotateAround(right, applied);

        Set(eye, target, Vector3d.UnitZ);
    }

    /// <summary>Moves eye and target together.</summary>
    public void Translate(Vector3d delta)
    {
        Eye += delta;
        Target += delta;
    }

    /// <summary>Zooms toward (factor &lt; 1) or away from (factor &gt; 1) a world point.</summary>
    public void ZoomAbout(Vector3d point, double factor)
    {
        if (IsPerspective)
        {
            var newEye = point + (Eye - point) * factor;
            // Keep the target in front of the camera; shrink the eye-target distance proportionally.
            var newTarget = newEye + Direction * Math.Max(Distance * factor, 0.01);
            Eye = newEye;
            Target = newTarget;
        }
        else
        {
            // Keep the point under the cursor fixed while scaling the visible height.
            var offset = point - Target;
            var planar = offset - Direction * Vector3d.Dot(offset, Direction);
            var delta = planar * (1 - factor);
            Translate(delta);
            OrthoHeight *= factor;
        }
    }

    /// <summary>Points the camera along a standard direction while keeping the target.</summary>
    public void SetStandardView(StandardView view, Vector3d center, double radius)
    {
        var (dir, up) = view switch
        {
            StandardView.Top => (-Vector3d.UnitZ, Vector3d.UnitY),
            StandardView.Bottom => (Vector3d.UnitZ, Vector3d.UnitY),
            StandardView.Front => (Vector3d.UnitY, Vector3d.UnitZ),
            StandardView.Back => (-Vector3d.UnitY, Vector3d.UnitZ),
            StandardView.Left => (Vector3d.UnitX, Vector3d.UnitZ),
            StandardView.Right => (-Vector3d.UnitX, Vector3d.UnitZ),
            _ => (new Vector3d(-1, 1, -0.72).Normalized(), Vector3d.UnitZ),
        };
        var distance = Math.Max(Distance, radius * 3);
        Set(center - dir * distance, center, up);
    }

    /// <summary>Frames a bounding sphere so it fills the view.</summary>
    public void ZoomExtents(Vector3d center, double radius, double aspect)
    {
        radius = Math.Max(radius, 1);
        var halfFovY = FovYDegrees * Math.PI / 360;
        var halfFovX = Math.Atan(Math.Tan(halfFovY) * aspect);
        var halfFov = Math.Min(halfFovY, halfFovX);
        var distance = radius / Math.Sin(halfFov) * 1.05;
        var dir = Direction;
        Set(center - dir * distance, center, Up);
        if (!IsPerspective)
            OrthoHeight = 2 * radius * 1.05 * Math.Max(1, 1 / aspect);
    }

    /// <summary>Matches the parallel view height to what the perspective view shows at the target.</summary>
    public void SyncOrthoHeightFromPerspective() =>
        OrthoHeight = 2 * Distance * Math.Tan(FovYDegrees * Math.PI / 360);
}

public enum StandardView
{
    Iso,
    Top,
    Bottom,
    Front,
    Back,
    Left,
    Right,
}
