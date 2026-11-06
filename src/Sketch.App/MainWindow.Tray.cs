using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sketch.App.Commands;
using Sketch.App.Panels;
using Sketch.Model;
using Sketch.Tools;

namespace Sketch.App;

/// <summary>Tray panels, tag selector, group commands, display styles and the view's right-click menu.</summary>
public partial class MainWindow
{
    // ---------------- Tray ----------------

    private void BuildTray()
    {
        var model = Viewport.Model;
        _entityInfo = new EntityInfoPanel(model, Viewport.Selection);
        var materials = new MaterialsPanel(model, _paintTool, () => _tools.Select(_paintTool));
        var tags = new TagsPanel(model);
        var outliner = new OutlinerPanel(model, Viewport.Selection);

        Tray.Children.Add(Section("Entity Info", _entityInfo, expanded: true));
        _materialsSection = Section("Materials", materials, expanded: true);
        _tagsSection = Section("Tags", tags, expanded: false);
        Tray.Children.Add(_materialsSection);
        Tray.Children.Add(_tagsSection);
        _tagTool.ActiveTagName = () => model.ActiveTag?.Name ?? TagCollection.UntaggedName;
        model.DisplayChanged += (_, _) => RefreshToolState();
        Tray.Children.Add(Section("Outliner", outliner, expanded: true));

        _scenes = new ScenesBar(model, () => Viewport.Camera, () => Viewport.Invalidate());
        ScenesHost.Content = _scenes;
        _files.FileChanged += (_, _) =>
        {
            materials.Refresh();
            tags.Refresh();
            outliner.Refresh();
            _scenes.Refresh();
            RefreshTagCombo();
        };
    }

    private Expander _materialsSection = null!;
    private Expander _tagsSection = null!;
    private Tool? _lastActiveTool;

    /// <summary>Paint Bucket opens the Materials panel and the Tag tool the Tags panel, as in SketchUp.</summary>
    private void OpenPanelForTool()
    {
        var tool = _tools.ActiveTool;
        if (ReferenceEquals(tool, _lastActiveTool)) return;
        _lastActiveTool = tool;
        if (ReferenceEquals(tool, _paintTool)) _materialsSection.IsExpanded = true;
        if (ReferenceEquals(tool, _tagTool)) _tagsSection.IsExpanded = true;
    }

    private Expander Section(string title, UIElement content, bool expanded) => new()
    {
        Header = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 52, 59)) },
        Content = content,
        IsExpanded = expanded,
        Style = (Style)FindResource("TraySection"),
    };

    // ---------------- Tag selector (toolbar) ----------------

    private void SetupTagCombo()
    {
        TagCombo.ItemTemplate = PanelHelpers.TagItemTemplate();
        TagCombo.SelectionChanged += (_, _) =>
        {
            if (_updatingTagCombo || TagCombo.SelectedItem is not PanelHelpers.TagItem item) return;
            var model = Viewport.Model;
            var items = Viewport.Selection.Items.Where(e => e is not Vertex && !e.IsDeleted).ToList();
            if (items.Count > 0)
            {
                // With a selection, the selector re-tags the selected entities (like SketchUp's Tags toolbar).
                model.Run("Tag", () =>
                {
                    foreach (var e in items) e.Tag = item.Tag;
                    model.Entities.Touch();
                });
            }
            else
            {
                model.ActiveTag = item.Tag;
                model.RaiseDisplayChanged();
            }
            FocusSinkFocus();
        };
        Viewport.Selection.Changed += (_, _) => RefreshTagCombo();
        Viewport.Model.Tags.Changed += (_, _) => RefreshTagCombo();
        Viewport.Model.DisplayChanged += (_, _) => RefreshTagCombo();
        Viewport.Model.Entities.Changed += (_, _) => RefreshTagCombo();
        RefreshTagCombo();
    }

    private void RefreshTagCombo()
    {
        _updatingTagCombo = true;
        try
        {
            var model = Viewport.Model;
            var items = PanelHelpers.SetTagItems(TagCombo, model);
            var selected = Viewport.Selection.Items.Where(e => e is not Vertex && !e.IsDeleted).ToList();
            Tag? shown;
            if (selected.Count == 0) shown = model.ActiveTag;
            else
            {
                var tags = selected.Select(e => e.Tag).Distinct().ToList();
                if (tags.Count != 1)
                {
                    TagCombo.SelectedItem = null;
                    return;
                }
                shown = tags[0];
            }
            TagCombo.SelectedItem = items.FirstOrDefault(i => i.Tag == shown);
        }
        finally
        {
            _updatingTagCombo = false;
        }
    }

    // ---------------- Group commands ----------------

    private void MakeGroup()
    {
        var model = Viewport.Model;
        var items = Viewport.Selection.Items.Where(e => !e.IsDeleted && ReferenceEquals(e.Parent, model.ActiveEntities)).ToList();
        if (items.Count == 0) return;
        _tools.CancelActive();
        Group? group = null;
        model.Run("Make Group", () => group = model.ActiveEntities.MakeGroup(items));
        if (group is not null) Viewport.Selection.Set(new[] { group });
    }

    private void ExplodeSelection()
    {
        var model = Viewport.Model;
        var groups = Viewport.Selection.Items.OfType<Group>().Where(g => !g.IsDeleted).ToList();
        if (groups.Count == 0) return;
        var result = new List<Entity>();
        model.Run("Explode", () =>
        {
            foreach (var g in groups) result.AddRange(g.Parent!.Explode(g));
        });
        Viewport.Selection.Set(result.Where(e => !e.IsDeleted));
    }

    private void OpenSelectedGroup()
    {
        if (Viewport.Selection.Items.OfType<Group>().FirstOrDefault() is not { } g) return;
        Viewport.Model.EnterGroup(g);
        Viewport.Selection.Clear();
    }

    private void SetHidden(bool hidden)
    {
        var items = Viewport.Selection.Items.Where(e => e is not Vertex && !e.IsDeleted).ToList();
        if (items.Count == 0 && hidden)
        {
            // Nothing selected: hide whatever is under the mouse.
            var q = new Sketch.Inference.InferenceQuery(Viewport.Camera, Viewport.PixelWidth, Viewport.PixelHeight, Viewport.LastMouseX, Viewport.LastMouseY);
            var deep = Viewport.Inference.PickEntity(q);
            var picked = deep is null ? null : Entities.AncestorIn(deep, Viewport.Model.ActiveEntities);
            if (picked is not null) items.Add(picked);
        }
        if (items.Count == 0) return;
        Viewport.Model.Run(hidden ? "Hide" : "Unhide", () =>
        {
            foreach (var e in items) e.Hidden = hidden;
            Viewport.Model.Entities.Touch();
        });
        if (hidden) Viewport.Selection.Clear();
    }

    private void UnhideAll()
    {
        var model = Viewport.Model;
        var container = model.ActiveEntities;
        model.Run("Unhide All", () =>
        {
            Unhide(container);
            model.Entities.Touch();
        });

        static void Unhide(Entities c)
        {
            foreach (var e in c.Edges.Cast<Entity>().Concat(c.Faces).Concat(c.Groups).Concat(c.Guides).Concat(c.Dimensions)
                         .Concat(c.Notes).Concat(c.Images))
                e.Hidden = false;
            foreach (var g in c.Groups) Unhide(g.Entities);
        }
    }

    // ---------------- Styles ----------------

    private void AddStyle(string key, string title, FaceStyle style) =>
        Add(new AppCommand
        {
            Key = key, Title = title, Description = $"Face style: {title}.",
            IconKey = "Icon.Style." + style,
            Execute = () => SetStyle(Viewport.Model.Style with { FaceStyle = style }),
            IsChecked = () => Viewport.Model.Style.FaceStyle == style,
        });

    private void SetStyle(RenderStyle style)
    {
        Viewport.Model.Style = style;
        Viewport.Model.MarkModified();
        RefreshToolState();
    }

    // ---------------- Image import ----------------

    private void ImportImageWithDialog()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == true) ImportImage(dialog.FileName);
    }

    /// <summary>Loads an image file into the model's image store and starts placing it.</summary>
    public void ImportImage(string path)
    {
        try
        {
            var bytes = System.IO.File.ReadAllBytes(path);
            var (w, h) = Sketch.App.Viewport.WpfImageDecoder.Size(bytes);
            var key = Viewport.Model.Context.Images.Add(bytes);
            _imageTool.Begin(key, w, h, System.IO.Path.GetFileName(path));
            _tools.Select(_imageTool);
            RefreshToolState();
        }
        catch (Exception ex) when (ex is System.IO.IOException or NotSupportedException or System.IO.FileFormatException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Could not open the image:\n{ex.Message}", "Sketch", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---------------- Right-click menu ----------------

    private void ShowViewContextMenu(double x, double y)
    {
        var model = Viewport.Model;
        if (Viewport.Selection.IsEmpty)
        {
            var deep = Viewport.Inference.PickEntity(new Sketch.Inference.InferenceQuery(Viewport.Camera, Viewport.PixelWidth, Viewport.PixelHeight, x, y));
            var picked = deep is null ? null : Entities.AncestorIn(deep, model.ActiveEntities);
            if (picked is not null) Viewport.Selection.Set(new[] { picked });
        }
        var selection = Viewport.Selection.Items.ToList();

        var menu = new ContextMenu();
        void Item(string key, bool enabled = true)
        {
            var cmd = _byKey[key];
            var mi = new MenuItem { Header = cmd.Title, InputGestureText = cmd.ShortcutText, IsEnabled = enabled };
            mi.Click += (_, _) => Run(cmd);
            menu.Items.Add(mi);
        }

        var hasGroup = selection.OfType<Group>().Any();
        if (selection.Count == 1 && hasGroup) Item("edit/open group");
        if (hasGroup) Item("edit/explode");
        if (selection.Count > 0) Item("edit/make group");
        if (model.EditPath.Count > 0) Item("edit/close group");
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        if (selection.OfType<Face>().Any()) Item("edit/reverse faces");
        if (selection.OfType<SectionPlane>().Any())
        {
            Item("edit/reverse section");
            var active = _byKey["edit/active section"];
            var mi = new MenuItem { Header = active.Title, IsCheckable = true, IsChecked = active.IsChecked!() };
            mi.Click += (_, _) => Run(active);
            menu.Items.Add(mi);
        }
        if (selection.Any(e => e is not SectionPlane and not Guide))
        {
            var flip = new MenuItem { Header = "Flip Along" };
            foreach (var (key, title) in new[] { ("edit/flip red", "Red Direction"), ("edit/flip green", "Green Direction"), ("edit/flip blue", "Blue Direction") })
            {
                var cmd = _byKey[key];
                var fi = new MenuItem { Header = title };
                fi.Click += (_, _) => Run(cmd);
                flip.Items.Add(fi);
            }
            menu.Items.Add(flip);
        }
        if (selection.Count > 0)
        {
            Item("edit/hide");
            Item("edit/delete");
        }
        Item("edit/unhide all");
        menu.Items.Add(new Separator());
        Item("camera/zoom extents");

        menu.PlacementTarget = Viewport;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }
}
