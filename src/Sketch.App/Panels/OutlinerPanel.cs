using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Sketch.Model;
using Sketch.Tools;

namespace Sketch.App.Panels;

/// <summary>
/// Outliner: the group tree (each top-level group is a root) with visibility eyes. Click selects, Ctrl+click adds or
/// removes, Shift+click selects the range from the last clicked group, double-click opens a group.
/// </summary>
public sealed class OutlinerPanel : DockPanel
{
    private readonly SketchModel _model;
    private readonly Selection _selection;
    private readonly TreeView _tree = new() { MaxHeight = 320, BorderThickness = new Thickness(0) };
    private readonly TextBox _filter = new() { Width = 220 };
    private bool _refreshQueued;
    /// <summary>Groups in the order the tree shows them (for Shift ranges).</summary>
    private readonly List<Group> _order = new();
    private Group? _anchor;

    public OutlinerPanel(SketchModel model, Selection selection)
    {
        _model = model;
        _selection = selection;
        Margin = new Thickness(8, 6, 8, 8);
        var filterRow = PanelHelpers.Row(PanelHelpers.Label("Filter:", 40), _filter);
        SetDock(filterRow, Dock.Top);
        Children.Add(filterRow);
        Children.Add(_tree);

        _filter.TextChanged += (_, _) => Refresh();
        model.Entities.Changed += (_, _) => QueueRefresh();
        model.EditContextChanged += (_, _) => QueueRefresh();
        model.Tags.Changed += (_, _) => QueueRefresh(); // swatch colours and names
        selection.Changed += (_, _) => QueueRefresh();
        Refresh();
    }

    /// <summary>Geometry changes arrive in bursts (live previews); rebuild the tree once per UI idle.</summary>
    private void QueueRefresh()
    {
        if (_refreshQueued) return;
        _refreshQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _refreshQueued = false;
            Refresh();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private string? _shown;

    /// <summary>
    /// Rebuilds the tree when what it shows changed (groups, names, visibility, tags, selection, edit path). Geometry
    /// edits inside groups do not change it: rebuilding 100+ rows on every mouse move of a tool made dragging stutter.
    /// </summary>
    public void Refresh()
    {
        var filter = _filter.Text.Trim();
        var shown = Signature(filter);
        if (shown == _shown) return;
        _shown = shown;
        _tree.Items.Clear();
        _order.Clear();
        AddGroups(_tree, _model.Entities, filter);
    }

    private string Signature(string filter)
    {
        var sb = new System.Text.StringBuilder(filter).Append('|');
        void Add(Entities container, int depth)
        {
            foreach (var g in container.Groups)
            {
                var tag = Entities.EffectiveTag(g);
                // The object itself counts too: undo recreates groups and the rows must point at the live ones.
                sb.Append(depth).Append(':').Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(g)).Append(':')
                    .Append(g).Append(':').Append(g.Hidden).Append(':').Append(tag?.Name).Append(':').Append(tag?.Color)
                    .Append(':').Append(_selection.Contains(g)).Append(':').Append(_model.EditPath.Contains(g)).Append(';');
                Add(g.Entities, depth + 1);
            }
        }
        Add(_model.Entities, 0);
        return sb.ToString();
    }

    private void AddGroups(ItemsControl parent, Entities container, string filter)
    {
        foreach (var g in container.Groups)
        {
            var label = g.ToString();
            var childMatches = g.Entities.AllGroups().Any(x => x.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase));
            if (filter.Length > 0 && !label.Contains(filter, StringComparison.OrdinalIgnoreCase) && !childMatches) continue;

            var item = new TreeViewItem { Header = Header(g, label), IsExpanded = true, Tag = g };
            _order.Add(g);
            if (_selection.Contains(g)) item.Background = new SolidColorBrush(Color.FromRgb(214, 230, 248));
            if (_model.EditPath.Contains(g)) item.FontWeight = FontWeights.Bold;
            item.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Click(g, Keyboard.Modifiers);
            };
            item.MouseDoubleClick += (_, e) =>
            {
                e.Handled = true;
                OpenGroup(g);
            };
            AddGroups(item, g.Entities, filter);
            parent.Items.Add(item);
        }
    }

    private FrameworkElement Header(Group g, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var eye = new CheckBox { IsChecked = !g.Hidden, ToolTip = "Visible", Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        eye.Click += (_, e) =>
        {
            var hide = eye.IsChecked != true;
            _model.Run(hide ? "Hide" : "Unhide", () =>
            {
                g.Hidden = hide;
                _model.Entities.Touch();
            });
        };
        row.Children.Add(eye);
        if (Entities.EffectiveTag(g) is { } tag)
        {
            var swatch = PanelHelpers.Swatch(tag.Color, 10);
            swatch.Margin = new Thickness(0, 0, 4, 0);
            swatch.ToolTip = tag.Name;
            row.Children.Add(swatch);
        }
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    /// <summary>
    /// Click selects the group (opening its parent for editing if needed); Ctrl adds it to or removes it from the
    /// selection; Shift selects every group between the last clicked one and this one. Groups can only be selected
    /// together when they share a parent, as in the model view.
    /// </summary>
    private void Click(Group g, ModifierKeys modifiers)
    {
        var sameContext = ReferenceEquals(g.Parent, _model.ActiveEntities);
        if (modifiers.HasFlag(ModifierKeys.Shift) && _anchor is { IsDeleted: false } anchor && ReferenceEquals(anchor.Parent, g.Parent))
        {
            var a = _order.IndexOf(anchor);
            var b = _order.IndexOf(g);
            if (a >= 0 && b >= 0)
            {
                if (a > b) (a, b) = (b, a);
                var range = _order.Skip(a).Take(b - a + 1).Where(x => ReferenceEquals(x.Parent, g.Parent)).ToList();
                if (!sameContext) OpenContext(g.Parent!);
                if (modifiers.HasFlag(ModifierKeys.Control)) _selection.Add(range);
                else _selection.Set(range);
                return;
            }
        }
        if (modifiers.HasFlag(ModifierKeys.Control) && sameContext)
        {
            _selection.Toggle(new[] { g });
            _anchor = g;
            return;
        }
        SelectGroup(g);
        _anchor = g;
    }

    /// <summary>Selecting a group in the tree opens its parent for editing if needed, then selects it.</summary>
    private void SelectGroup(Group g)
    {
        OpenContext(g.Parent!);
        _selection.Set(new[] { g });
    }

    private void OpenGroup(Group g)
    {
        OpenContext(g.Entities);
        _selection.Clear();
    }

    private void OpenContext(Entities container)
    {
        var chain = new List<Group>();
        for (var o = container.Owner; o is not null; o = o.Parent?.Owner) chain.Insert(0, o);
        _model.ExitAllGroups();
        foreach (var grp in chain) _model.EnterGroup(grp);
    }
}
