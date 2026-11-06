using System.Globalization;

namespace Sketch.Geometry;

public enum LengthUnit
{
    Millimeters,
    Centimeters,
    Meters,
    Inches,
    Feet,
}

/// <summary>
/// Formats and parses lengths. Internal unit is the inch. Typed values without a suffix use the display unit;
/// suffixes mm, cm, m, ", ' are accepted. Both '.' and ',' are accepted as decimal separators when unambiguous.
/// </summary>
public sealed class Units
{
    public static Units Default { get; set; } = new();

    public LengthUnit Unit { get; set; } = LengthUnit.Millimeters;
    public int Precision { get; set; } = 0;

    public static double InchesPer(LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeters => 1 / 25.4,
        LengthUnit.Centimeters => 1 / 2.54,
        LengthUnit.Meters => 1 / 0.0254,
        LengthUnit.Feet => 12,
        _ => 1,
    };

    public static string Suffix(LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeters => "mm",
        LengthUnit.Centimeters => "cm",
        LengthUnit.Meters => "m",
        LengthUnit.Feet => "'",
        _ => "\"",
    };

    public string Format(double inches)
    {
        var value = inches / InchesPer(Unit);
        var rounded = Math.Round(value, Precision);
        if (rounded == 0) rounded = 0; // no "-0"
        var text = rounded.ToString("F" + Precision, CultureInfo.InvariantCulture);
        var approx = Math.Abs(rounded - value) > 1e-9 * Math.Max(1, Math.Abs(value)) ? "~ " : string.Empty;
        var sep = Unit is LengthUnit.Inches or LengthUnit.Feet ? string.Empty : " ";
        return approx + text + sep + Suffix(Unit);
    }

    /// <summary>Formats an area given in square inches, e.g. "13435000 mm²".</summary>
    public string FormatArea(double squareInches)
    {
        var per = InchesPer(Unit);
        var value = squareInches / (per * per);
        var precision = Unit is LengthUnit.Meters ? 2 : Precision;
        var suffix = Unit switch
        {
            LengthUnit.Inches => " in²",
            LengthUnit.Feet => " ft²",
            _ => " " + Suffix(Unit) + "²",
        };
        return Math.Round(value, precision).ToString("F" + precision, CultureInfo.InvariantCulture) + suffix;
    }

    /// <summary>Parses a single length such as "1200", "1.2m", "45cm", "12\"", "3'". Returns inches.</summary>
    public bool TryParseLength(string text, out double inches)
    {
        inches = 0;
        var t = text.Trim().ToLowerInvariant().Replace(" ", string.Empty);
        if (t.Length == 0) return false;

        var unit = Unit;
        foreach (var (suffix, u) in new[]
                 {
                     ("mm", LengthUnit.Millimeters), ("cm", LengthUnit.Centimeters), ("m", LengthUnit.Meters),
                     ("\"", LengthUnit.Inches), ("in", LengthUnit.Inches), ("'", LengthUnit.Feet), ("ft", LengthUnit.Feet),
                 })
        {
            if (t.EndsWith(suffix))
            {
                unit = u;
                t = t[..^suffix.Length];
                break;
            }
        }

        if (!TryParseNumber(t, out var value)) return false;
        inches = value * InchesPer(unit);
        return true;
    }

    public static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Splits a Measurements-box entry into parts. ';' always separates; ',' separates too unless the entry is a
    /// single decimal number like "2,5".
    /// </summary>
    public static string[] SplitList(string text)
    {
        var t = text.Trim().Trim('[', ']', '<', '>');
        if (t.Contains(';')) return t.Split(';', StringSplitOptions.TrimEntries);
        var parts = t.Split(',', StringSplitOptions.TrimEntries);
        return parts;
    }
}
