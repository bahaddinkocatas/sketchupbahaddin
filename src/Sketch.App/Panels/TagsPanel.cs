using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Sketch.Model;
using Sketch.Model.Templates;

namespace Sketch.App.Panels;

/// <summary>
/// Tags: visibility eye, active-tag pencil, colour swatch and name for each tag; add, delete, rename, filter and
/// the "Color by tag" switch.
/// </summary>
public sealed class TagsPanel : DockPanel
{
    private readonly SketchModel _model;
    private readonly StackPanel _list = new();
    private readonly TextBox _filter = new() { Margin = new Thickness(0, 0, 0, 4) };
    private readonly ToggleButton _colorByTag = new() { Content = "Color by Tag", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 4, 0) };
    private Tag? _selected;

    public TagsPanel(SketchModel model)
    {
        _model = model;
        Margin = new Thickness(8, 6, 8, 8);
        LastChildFill = true;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var add = new Button { Content = "+ Add Tag", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 4, 0) };
        var del = new Button { Content = "Delete Tag", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 4, 0) };
        buttons.Children.Add(add);
        buttons.Children.Add(del);
        buttons.Children.Add(_colorByTag);
        SetDock(buttons, Dock.Top);
        Children.Add(buttons);

        var filterRow = PanelHelpers.Row(PanelHelpers.Label("Search:", 50), _filter);
        _filter.Width = 220;
        SetDock(filterRow, Dock.Top);
        Children.Add(filterRow);

        var scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 360 };
        Children.Add(scroll);

        add.Click += (_, _) =>
        {
            var name = Prompt.Show(Window.GetWindow(this), "New tag name:", "Add Tag");
            if (string.IsNullOrWhiteSpace(name) || name == TagCollection.UntaggedName) return;
            Tag? tag = null;
            _model.Run("Add Tag", () => tag = _model.Tags.Add(name, ThermalTemplate.DefaultColor(name, _model.Tags.Items.Count)));
            _model.ActiveTag = tag;
        };
        del.Click += (_, _) =>
        {
            if (_selected is not { } tag) return;
            var answer = MessageBox.Show(Window.GetWindow(this),
                $"Delete the tag \"{tag.Name}\"? Entities that use this tag will become Untagged.", "Delete Tag",
                MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer != MessageBoxResult.OK) return;
            _model.Run("Delete Tag", () =>
            {
                _model.Entities.ReplaceTag(tag, null);
                _model.Tags.Remove(tag);
            });
            _selected = null;
        };
        _colorByTag.Click += (_, _) =>
        {
            _model.Style = _model.Style with { ColorByTag = _colorByTag.IsChecked == true };
            _model.MarkModified();
        };
        _filter.TextChanged += (_, _) => Refresh();
        model.Tags.Changed += (_, _) => Refresh();
        model.DisplayChanged += (_, _) =>
        {
            _colorByTag.IsChecked = _model.Style.ColorByTag;
            if (_shownActive != _model.ActiveTag) Refresh();
        };
        Refresh();
    }

    private Tag? _shownActive;

    public void Refresh()
    {
        _shownActive = _model.ActiveTag;
        _colorByTag.IsChecked = _model.Style.ColorByTag;
        _list.Children.Clear();
        var filter = _filter.Text.Trim();
        _list.Children.Add(RowFor(null));
        foreach (var tag in _model.Tags.Items)
            if (filter.Length == 0 || tag.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                _list.Children.Add(RowFor(tag));
    }

    private FrameworkElement RowFor(Tag? tag)
    {
        var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1), Background = Brushes.Transparent };
        if (tag is not null && tag == _selected) row.Background = new SolidColorBrush(Color.FromRgb(214, 230, 248));

        var eye = new CheckBox
        {
            IsChecked = tag?.Visible ?? true,
            IsEnabled = tag is not null,
            ToolTip = "Visible",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        eye.Click += (_, _) =>
        {
            if (tag is null) return;
            _model.Tags.SetVisible(tag, eye.IsChecked == true);
            _model.MarkModified();
        };

        var active = new RadioButton
        {
            GroupName = "activeTag",
            IsChecked = _model.ActiveTag == tag,
            ToolTip = "Active Tag (new geometry is placed on this tag)",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        active.Checked += (_, _) =>
        {
            if (_model.ActiveTag == tag) return;
            _model.ActiveTag = tag;
            _model.RaiseDisplayChanged();
        };

        var swatch = PanelHelpers.Swatch(tag?.Color ?? TagCollection.UntaggedColor);
        swatch.Margin = new Thickness(0, 0, 6, 0);
        if (tag is not null)
        {
            swatch.Cursor = Cursors.Hand;
            swatch.ToolTip = "Change Color";
            swatch.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (ColorPicker.Show(Window.GetWindow(this), tag.Color) is not { } color) return;
                _model.Run("Tag Color", () =>
                {
                    tag.Color = color;
                    _model.Tags.RaiseChanged();
                });
            };
        }

        var name = new TextBlock { Text = tag?.Name ?? TagCollection.UntaggedName, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(eye, Dock.Left);
        DockPanel.SetDock(active, Dock.Left);
        DockPanel.SetDock(swatch, Dock.Left);
        row.Children.Add(eye);
        row.Children.Add(active);
        row.Children.Add(swatch);
        row.Children.Add(name);

        row.MouseLeftButtonDown += (_, e) =>
        {
            _selected = tag;
            if (e.ClickCount == 2 && tag is not null)
            {
                var newName = Prompt.Show(Window.GetWindow(this), "New tag name:", "Rename Tag", tag.Name);
                if (!string.IsNullOrWhiteSpace(newName)) _model.Run("Rename Tag", () => _model.Tags.Rename(tag, newName));
            }
            Refresh();
        };
        return row;
    }
}

/// <summary>Minimal text input dialog.</summary>
public static class Prompt
{
    public static string? Show(Window? owner, string message, string title, string initial = "")
    {
        var box = new TextBox { Text = initial, Margin = new Thickness(0, 6, 0, 10), MinWidth = 280 };
        var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Margin = new Thickness(0, 0, 6, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 80 };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock { Text = message });
        panel.Children.Add(box);
        panel.Children.Add(buttons);
        var window = new Window
        {
            Title = title,
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Owner = owner,
            ShowInTaskbar = false,
        };
        ok.Click += (_, _) => window.DialogResult = true;
        window.Loaded += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };
        return window.ShowDialog() == true ? box.Text.Trim() : null;
    }
}
