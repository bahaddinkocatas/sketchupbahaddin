using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sketch.Geometry;

namespace Sketch.App.Panels;

/// <summary>"3D Metin" dialog: text, font, height, filled/extruded. Produces glyph outlines for the placement tool.</summary>
public static class Text3DDialog
{
    public sealed record Result(string Text, IReadOnlyList<IReadOnlyList<Vector2d>> Loops, bool Filled, double Extrude);

    private static string _lastText = "Text";
    private static string _lastFont = "Arial";
    private static bool _lastBold;
    private static string _lastHeight = "300";
    private static string _lastDepth = "50";
    private static bool _lastFilled = true;
    private static bool _lastExtruded = true;

    public static Result? Show(Window owner)
    {
        var text = new TextBox { Text = _lastText, AcceptsReturn = true, Height = 70, MinWidth = 320, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        var fonts = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(n => n).ToList();
        var font = new ComboBox { ItemsSource = fonts, SelectedItem = fonts.Contains(_lastFont) ? _lastFont : fonts.FirstOrDefault(), Width = 200 };
        var bold = new CheckBox { Content = "Bold", IsChecked = _lastBold, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var height = new TextBox { Text = _lastHeight, Width = 90 };
        var filled = new CheckBox { Content = "Filled", IsChecked = _lastFilled };
        var extruded = new CheckBox { Content = "Extruded", IsChecked = _lastExtruded, Margin = new Thickness(0, 4, 0, 0) };
        var depth = new TextBox { Text = _lastDepth, Width = 90, Margin = new Thickness(8, 4, 0, 0) };

        StackPanel Row(string label, params UIElement[] items)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(new TextBlock { Text = label, Width = 90, VerticalAlignment = VerticalAlignment.Center });
            foreach (var i in items) row.Children.Add(i);
            return row;
        }

        var place = new Button { Content = "Place", IsDefault = true, Width = 90, Margin = new Thickness(0, 0, 6, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 80 };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(place);
        buttons.Children.Add(cancel);

        var extrudeRow = new StackPanel { Orientation = Orientation.Horizontal };
        extrudeRow.Children.Add(extruded);
        extrudeRow.Children.Add(depth);

        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(text);
        panel.Children.Add(Row("Font:", font, bold));
        panel.Children.Add(Row("Height:", height, new TextBlock { Text = " (letter height)", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray }));
        panel.Children.Add(Row("Form:", filled));
        panel.Children.Add(Row(string.Empty, extrudeRow));
        panel.Children.Add(buttons);

        var window = new Window
        {
            Title = "Place 3D Text",
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ShowInTaskbar = false,
        };
        place.Click += (_, _) => window.DialogResult = true;
        window.Loaded += (_, _) =>
        {
            text.Focus();
            text.SelectAll();
        };
        if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(text.Text)) return null;

        if (!Units.Default.TryParseLength(height.Text, out var h) || h <= 0) h = 300;
        double d = 0;
        if (extruded.IsChecked == true && filled.IsChecked == true && (!Units.Default.TryParseLength(depth.Text, out d) || d < 0)) d = 0;

        _lastText = text.Text;
        _lastFont = font.SelectedItem as string ?? "Arial";
        _lastBold = bold.IsChecked == true;
        _lastHeight = height.Text;
        _lastDepth = depth.Text;
        _lastFilled = filled.IsChecked == true;
        _lastExtruded = extruded.IsChecked == true;

        var loops = Outlines(text.Text.Replace("\r", string.Empty), _lastFont, _lastBold, h);
        return new Result(text.Text, loops, filled.IsChecked == true, extruded.IsChecked == true ? d : 0);
    }

    /// <summary>Flattened glyph outlines; capital letters are <paramref name="capHeight"/> tall, the first baseline is y = 0.</summary>
    public static List<IReadOnlyList<Vector2d>> Outlines(string text, string fontName, bool bold, double capHeight)
    {
        const double em = 100;
        var family = new FontFamily(fontName);
        var typeface = new Typeface(family, FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
        var caps = typeface.TryGetGlyphTypeface(out var glyphs) ? glyphs.CapsHeight : 0.7;
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, em, Brushes.Black, 1.0);
        var scale = capHeight / (caps * em);
        var baseline = formatted.Baseline;
        var geometry = formatted.BuildGeometry(new Point(0, 0)).GetFlattenedPathGeometry(0.05, ToleranceType.Absolute);

        var loops = new List<IReadOnlyList<Vector2d>>();
        foreach (var figure in geometry.Figures)
        {
            var pts = new List<Point> { figure.StartPoint };
            foreach (var seg in figure.Segments)
            {
                switch (seg)
                {
                    case LineSegment l: pts.Add(l.Point); break;
                    case PolyLineSegment pl: pts.AddRange(pl.Points); break;
                }
            }
            // Drop repeated points (including the closing one).
            var clean = new List<Vector2d>();
            foreach (var p in pts)
            {
                var v = new Vector2d(p.X * scale, (baseline - p.Y) * scale);
                if (clean.Count == 0 || (clean[^1] - v).Length > 1e-3) clean.Add(v);
            }
            if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 1e-3) clean.RemoveAt(clean.Count - 1);
            if (clean.Count >= 3) loops.Add(clean);
        }
        return loops;
    }
}
