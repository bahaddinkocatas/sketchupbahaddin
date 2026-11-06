using Sketch.Geometry;

namespace Sketch.Tools.Drawing;

/// <summary>Parses Measurements-box entries shared by several tools.</summary>
public static class MeasurementInput
{
    /// <summary>"[x,y,z]" absolute or "&lt;x,y,z&gt;" relative coordinates (lengths in display units).</summary>
    public static bool TryParseCoordinates(string text, out Vector3d value, out bool relative)
    {
        value = Vector3d.Zero;
        var t = text.Trim();
        relative = t.StartsWith('<');
        var absolute = t.StartsWith('[');
        if (!relative && !absolute) return false;
        var parts = Units.SplitList(t);
        if (parts.Length != 3) return false;
        var v = new double[3];
        for (var i = 0; i < 3; i++)
            if (!Units.Default.TryParseLength(parts[i], out v[i])) return false;
        value = new Vector3d(v[0], v[1], v[2]);
        return true;
    }

    /// <summary>Array suffixes typed after a copy: "5x" or "*5" (multiply), "/5" (divide).</summary>
    public static bool TryParseArray(string text, out int count, out bool divide)
    {
        count = 0;
        divide = false;
        var t = text.Trim().ToLowerInvariant();
        if (t.StartsWith('/'))
        {
            divide = true;
            return int.TryParse(t[1..], out count) && count > 0;
        }
        if (t.EndsWith('x') || t.EndsWith('*')) return int.TryParse(t[..^1], out count) && count > 0;
        if (t.StartsWith('*') || t.StartsWith('x')) return int.TryParse(t[1..], out count) && count > 0;
        return false;
    }

    /// <summary>Segment count such as "24s".</summary>
    public static bool TryParseSegments(string text, out int segments)
    {
        segments = 0;
        var t = text.Trim().ToLowerInvariant();
        return t.EndsWith('s') && int.TryParse(t[..^1], out segments) && segments >= 3 && segments <= 999;
    }
}
