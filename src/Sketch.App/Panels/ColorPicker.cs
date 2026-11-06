using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Sketch.Model;

namespace Sketch.App.Panels;

/// <summary>Small colour picker: a palette plus a hex field.</summary>
public static class ColorPicker
{
    private static readonly string[] Palette =
    {
        "#000000", "#404040", "#808080", "#B0B0B0", "#D9D9D9", "#FFFFFF",
        "#7F0000", "#C9303C", "#E8505B", "#F27983", "#F5A3AA", "#FFD6DA",
        "#8B4500", "#CC5500", "#EE7600", "#FF9933", "#FFB266", "#FFE0C2",
        "#8B7500", "#B8860B", "#D4A017", "#E6C200", "#F0D264", "#FFF3B0",
        "#0B5E5A", "#0F7F7A", "#14A39B", "#3CC2B5", "#7FD6CC", "#D2F4EF",
        "#1B5E20", "#2E8B57", "#3CB371", "#66CDAA", "#8FBC8F", "#DFF5E1",
        "#0D2C6C", "#1F4E9C", "#2F7FD8", "#5FA8F0", "#8FB8DE", "#DCE9F8",
        "#3A4257", "#4F5A75", "#66708F", "#7D87A6", "#A3ABC4", "#E1E4EE",
        "#3B2152", "#523A73", "#6A4C93", "#8466AD", "#9E82C6", "#E6DCF3",
    };

    public static Rgb? Show(Window? owner, Rgb initial)
    {
        Rgb? result = null;
        var current = initial;
        var preview = new Border { Height = 28, Margin = new Thickness(0, 0, 0, 8), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Background = PanelHelpers.Brush(current) };
        var hex = new TextBox { Text = current.ToHex(), Width = 90, Margin = new Thickness(6, 0, 0, 0) };
        var grid = new UniformGrid { Columns = 6, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var h in Palette)
        {
            var c = Rgb.FromHex(h);
            var cell = new Border { Width = 26, Height = 22, Margin = new Thickness(1), Background = PanelHelpers.Brush(c), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Hand };
            cell.MouseLeftButtonUp += (_, _) =>
            {
                current = c;
                hex.Text = c.ToHex();
                preview.Background = PanelHelpers.Brush(c);
            };
            grid.Children.Add(cell);
        }
        hex.TextChanged += (_, _) =>
        {
            try
            {
                var t = hex.Text.Trim();
                if (t.TrimStart('#').Length != 6) return;
                current = Rgb.FromHex(t);
                preview.Background = PanelHelpers.Brush(current);
            }
            catch (FormatException)
            {
                // Keep the previous colour until the text is a valid hex code.
            }
        };

        var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Margin = new Thickness(0, 0, 6, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 80 };
        var buttons = PanelHelpers.Row(ok, cancel);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(preview);
        panel.Children.Add(grid);
        panel.Children.Add(PanelHelpers.Row(new TextBlock { Text = "Hex:", VerticalAlignment = VerticalAlignment.Center }, hex));
        panel.Children.Add(new Border { Height = 8 });
        panel.Children.Add(buttons);

        var window = new Window
        {
            Title = "Choose Color",
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            Owner = owner,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };
        ok.Click += (_, _) =>
        {
            result = current;
            window.DialogResult = true;
        };
        window.ShowDialog();
        return result;
    }
}

