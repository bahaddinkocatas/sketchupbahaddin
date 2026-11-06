using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sketch.Model;
using Sketch.Operations.Deap;
using Sketch.Tools.Modify;

namespace Sketch.App.Panels;

/// <summary>Materials: the model's materials as swatches; picking one activates the Paint Bucket with it.</summary>
public sealed class MaterialsPanel : StackPanel
{
    private readonly SketchModel _model;
    private readonly PaintBucketTool _paint;
    private readonly Action _activatePaint;
    private readonly WrapPanel _swatches = new() { Margin = new Thickness(0, 4, 0, 4) };
    private readonly Border _preview = new() { Height = 54, CornerRadius = new CornerRadius(3), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
    private readonly TextBlock _name = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Slider _opacity = new() { Minimum = 0.05, Maximum = 1, Width = 150, VerticalAlignment = VerticalAlignment.Center };
    private bool _updating;

    public MaterialsPanel(SketchModel model, PaintBucketTool paint, Action activatePaint)
    {
        _model = model;
        _paint = paint;
        _activatePaint = activatePaint;
        Margin = new Thickness(8, 6, 8, 8);
        Children.Add(_preview);
        Children.Add(PanelHelpers.Row(PanelHelpers.Label("Name:", 40), _name));
        Children.Add(PanelHelpers.Row(PanelHelpers.Label("Opacity:", 60), _opacity));
        Children.Add(_swatches);

        _opacity.ValueChanged += (_, _) =>
        {
            if (_updating || _paint.Current is not { } m) return;
            m.Alpha = _opacity.Value;
            _model.RaiseDisplayChanged();
            _model.MarkModified();
            Refresh();
        };
        model.Materials.Changed += (_, _) => Refresh();
        paint.CurrentChanged += (_, _) => Refresh();
        Refresh();
    }

    public void Refresh()
    {
        _updating = true;
        _swatches.Children.Clear();
        _swatches.Children.Add(Tile(null));
        // The DEAP label colour is the tool's own, not one to paint with.
        foreach (var m in _model.Materials.Items.Where(m => m.Name != DeapEvidence.AnnotationMaterialName)) _swatches.Children.Add(Tile(m));

        var current = _paint.Current;
        _name.Text = current?.Name ?? "Default";
        _preview.Background = current is null
            ? new LinearGradientBrush(Colors.White, Color.FromRgb(164, 178, 192), 0)
            : PanelHelpers.Brush(current.Color, current.Alpha);
        _opacity.IsEnabled = current is not null;
        _opacity.Value = current?.Alpha ?? 1;
        _updating = false;
    }

    private FrameworkElement Tile(Material? m)
    {
        var selected = m == _paint.Current;
        var border = new Border
        {
            Width = 54,
            Height = 54,
            Margin = new Thickness(2),
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(selected ? 3 : 1),
            BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(47, 127, 216)) : Brushes.Gray,
            Background = m is null
                ? new LinearGradientBrush(Colors.White, Color.FromRgb(164, 178, 192), 45)
                : PanelHelpers.Brush(m.Color, Math.Max(m.Alpha, 0.35)),
            ToolTip = m?.Name ?? "Default (remove material)",
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        border.MouseLeftButtonUp += (_, _) =>
        {
            _paint.Current = m;
            _activatePaint();
            Refresh();
        };
        return border;
    }
}
