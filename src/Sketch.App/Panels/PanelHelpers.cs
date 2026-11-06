using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sketch.Model;

namespace Sketch.App.Panels;

internal static class PanelHelpers
{
    public static SolidColorBrush Brush(Rgb c, double alpha = 1) =>
        new(Color.FromArgb((byte)Math.Round(alpha * 255), c.R, c.G, c.B));

    public static Border Swatch(Rgb c, double size = 14, double alpha = 1) => new()
    {
        Width = size,
        Height = size,
        Background = Brush(c, alpha),
        BorderBrush = Brushes.Gray,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(2),
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBlock Label(string text, double width = 70) => new()
    {
        Text = text,
        Width = width,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = new SolidColorBrush(Color.FromRgb(70, 74, 82)),
    };

    public static StackPanel Row(params UIElement[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    /// <summary>Tag list item for combo boxes: shows a colour swatch and the name; Untagged has a null tag.</summary>
    public sealed record TagItem(Tag? Tag)
    {
        public string Name => Tag?.Name ?? TagCollection.UntaggedName;
        public override string ToString() => Name;
    }

    public static DataTemplate TagItemTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(StackPanel));
        factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var swatch = new FrameworkElementFactory(typeof(Border));
        swatch.SetValue(FrameworkElement.WidthProperty, 12.0);
        swatch.SetValue(FrameworkElement.HeightProperty, 12.0);
        swatch.SetValue(Border.BorderBrushProperty, Brushes.Gray);
        swatch.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        swatch.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 0));
        swatch.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Tag.Color")
        {
            Converter = new RgbBrushConverter(),
            FallbackValue = Brushes.White,
        });
        factory.AppendChild(swatch);
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
        factory.AppendChild(text);
        return new DataTemplate { VisualTree = factory };
    }

    private sealed class RgbBrushConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            value is Rgb c ? Brush(c) : Brushes.White;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public static List<TagItem> TagItems(SketchModel model) =>
        model.Tags.Items.Select(t => new TagItem(t)).Prepend(new TagItem(null)).ToList();

    /// <summary>
    /// Gives a tag combo the model's tag list, but only when the tags (or their names or colours) changed, and
    /// returns the list shown. Replacing the list rebuilds the combo, which on every geometry change made dragging
    /// tools stutter.
    /// </summary>
    public static List<TagItem> SetTagItems(ComboBox combo, SketchModel model)
    {
        var key = string.Join("|", model.Tags.Items.Select(t =>
            $"{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(t)}:{t.Name}:{t.Color}"));
        if (combo.ItemsSource is List<TagItem> shown && combo.Tag is string old && old == key) return shown;
        var items = TagItems(model);
        combo.ItemsSource = items;
        combo.Tag = key;
        return items;
    }
}
