using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sketch.Geometry;
using Sketch.Model;
using Sketch.Tools;

namespace Sketch.App.Panels;

/// <summary>Entity Info: type, tag, area/length, material, group name and visibility of the selection.</summary>
public sealed class EntityInfoPanel : StackPanel
{
    private readonly SketchModel _model;
    private readonly Selection _selection;
    private readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
    private readonly ComboBox _tag = new() { Width = 200 };
    private readonly TextBlock _measure = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _measureLabel = PanelHelpers.Label("Area:");
    private readonly TextBlock _nameLabel = PanelHelpers.Label("Name:");
    private readonly TextBlock _material = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _name = new() { Width = 200 };
    private readonly CheckBox _hidden = new() { Content = "Hidden", Margin = new Thickness(0, 2, 12, 0) };
    private readonly StackPanel _tagRow, _measureRow, _materialRow, _nameRow, _toggleRow;
    private bool _updating;

    public EntityInfoPanel(SketchModel model, Selection selection)
    {
        _model = model;
        _selection = selection;
        Margin = new Thickness(8, 6, 8, 8);
        _tag.ItemTemplate = PanelHelpers.TagItemTemplate();

        Children.Add(_title);
        _tagRow = PanelHelpers.Row(PanelHelpers.Label("Tag:"), _tag);
        _nameRow = PanelHelpers.Row(_nameLabel, _name);
        _measureRow = PanelHelpers.Row(_measureLabel, _measure);
        _materialRow = PanelHelpers.Row(PanelHelpers.Label("Material:"), _material);
        _toggleRow = PanelHelpers.Row(PanelHelpers.Label("Toggles:"), _hidden);
        foreach (var r in new[] { _tagRow, _nameRow, _measureRow, _materialRow, _toggleRow }) Children.Add(r);

        _tag.SelectionChanged += (_, _) =>
        {
            if (_updating || _tag.SelectedItem is not PanelHelpers.TagItem item) return;
            var items = Targets();
            if (items.Count == 0) return;
            _model.Run("Tag", () =>
            {
                foreach (var e in items) e.Tag = item.Tag;
                _model.Entities.Touch();
            });
        };
        _name.LostFocus += (_, _) => CommitName();
        _name.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) CommitName();
        };
        _hidden.Click += (_, _) =>
        {
            var items = Targets();
            var hide = _hidden.IsChecked == true;
            _model.Run(hide ? "Hide" : "Unhide", () =>
            {
                foreach (var e in items) e.Hidden = hide;
                _model.Entities.Touch();
            });
        };

        selection.Changed += (_, _) => Refresh();
        model.Entities.Changed += (_, _) => Refresh();
        model.Tags.Changed += (_, _) => Refresh();
        Refresh();
    }

    private List<Entity> Targets() => _selection.Items.Where(e => !e.IsDeleted && e is not Vertex).ToList();

    private void CommitName()
    {
        if (_updating) return;
        var name = _name.Text;
        switch (_selection.Items.SingleOrDefault())
        {
            case Group g when g.Name != name:
                _model.Run("Rename", () =>
                {
                    g.Name = name;
                    _model.Entities.Touch();
                });
                break;
            case ImageEntity im when im.Name != name:
                _model.Run("Rename", () =>
                {
                    im.Name = name;
                    _model.Entities.Touch();
                });
                break;
            case Note n when n.Text != name && !string.IsNullOrWhiteSpace(name):
                _model.Run("Edit Text", () =>
                {
                    n.Text = name;
                    _model.Entities.Touch();
                });
                break;
        }
    }

    public void Refresh()
    {
        _updating = true;
        try
        {
            var items = Targets();
            var units = Units.Default;
            PanelHelpers.SetTagItems(_tag, _model);

            _title.Text = items.Count switch
            {
                0 => "No Selection",
                1 => items[0] switch
                {
                    Face => "Face",
                    Edge => "Edge",
                    Group g => string.IsNullOrEmpty(g.Name) ? "Group" : $"Group ({g.Name})",
                    Dimension d => $"Dimension ({units.Format(d.Length)})",
                    Note => "Text",
                    ImageEntity im => $"Image ({units.Format(im.U.Length)} × {units.Format(im.V.Length)})",
                    Guide gd => gd.IsPoint ? "Guide Point" : "Guide Line",
                    _ => "Entity",
                },
                _ => $"{items.Count} Entities" + Summary(items),
            };

            Visibility rowVis(bool show) => show ? Visibility.Visible : Visibility.Collapsed;
            _tagRow.Visibility = rowVis(items.Count > 0);
            _toggleRow.Visibility = rowVis(items.Count > 0);
            if (items.Count == 0)
            {
                _nameRow.Visibility = _measureRow.Visibility = _materialRow.Visibility = Visibility.Collapsed;
                return;
            }

            // Tag: shown when all selected items share it.
            var tags = items.Select(i => i.Tag).Distinct().ToList();
            _tag.SelectedItem = tags.Count == 1
                ? ((List<PanelHelpers.TagItem>)_tag.ItemsSource).FirstOrDefault(t => t.Tag == tags[0])
                : null;

            var faces = items.OfType<Face>().ToList();
            var edges = items.OfType<Edge>().ToList();
            if (faces.Count > 0)
            {
                _measureLabel.Text = "Area:";
                _measure.Text = units.FormatArea(faces.Sum(f => Math.Abs(f.Area)));
                _measureRow.Visibility = Visibility.Visible;
            }
            else if (edges.Count > 0)
            {
                _measureLabel.Text = "Length:";
                _measure.Text = units.Format(edges.Sum(e => e.Length));
                _measureRow.Visibility = Visibility.Visible;
            }
            else if (items.OfType<Group>().ToList() is { Count: > 0 } groups)
            {
                _measureLabel.Text = "Face Area:";
                _measure.Text = units.FormatArea(groups.SelectMany(g => g.Entities.AllFaces()).Sum(f => Math.Abs(f.Area)));
                _measureRow.Visibility = Visibility.Visible;
            }
            else
            {
                _measureRow.Visibility = Visibility.Collapsed;
            }

            var materials = faces.Select(f => f.Material?.Name ?? "(Default)")
                .Concat(items.OfType<Group>().Select(g => g.Material?.Name ?? "(Default)")).Distinct().ToList();
            _materialRow.Visibility = rowVis(materials.Count > 0);
            _material.Text = materials.Count == 1 ? materials[0] : "(Mixed)";

            var single = items.Count == 1 ? items[0] as Group : null;
            var note = items.Count == 1 ? items[0] as Note : null;
            var image = items.Count == 1 ? items[0] as ImageEntity : null;
            _nameRow.Visibility = rowVis(single is not null || note is not null || image is not null);
            _nameLabel.Text = note is not null ? "Text:" : "Name:";
            if (!_name.IsKeyboardFocusWithin) _name.Text = single?.Name ?? note?.Text ?? image?.Name ?? string.Empty;

            _hidden.IsChecked = items.All(i => i.Hidden) ? true : items.Any(i => i.Hidden) ? null : false;
        }
        finally
        {
            _updating = false;
        }
    }

    private static string Summary(List<Entity> items)
    {
        var parts = new List<string>();
        var f = items.Count(i => i is Face);
        var e = items.Count(i => i is Edge);
        var g = items.Count(i => i is Group);
        if (f > 0) parts.Add(f == 1 ? "1 Face" : $"{f} Faces");
        if (e > 0) parts.Add(e == 1 ? "1 Edge" : $"{e} Edges");
        if (g > 0) parts.Add(g == 1 ? "1 Group" : $"{g} Groups");
        return parts.Count > 0 ? $" ({string.Join(", ", parts)})" : string.Empty;
    }
}
