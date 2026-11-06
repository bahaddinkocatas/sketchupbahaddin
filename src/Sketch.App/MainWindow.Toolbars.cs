using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Sketch.App.Commands;
using Sketch.Geometry;
using Sketch.Model;
using Sketch.Tools;
using Sketch.Tools.Construction;
using Sketch.Tools.Drawing;
using Sketch.Tools.Modify;

namespace Sketch.App;

/// <summary>
/// Tools added after the first release (Lasso, Freehand, rotated rectangle, arcs, Flip, Axes, Section Plane,
/// 3D Text, Search) and the toolbars laid out like SketchUp's "Getting Started" (top) and "Large Tool Set"
/// (left) toolbars.
/// </summary>
public partial class MainWindow
{
    private readonly LassoTool _lassoTool = new();
    private readonly FreehandTool _freehandTool = new();
    private readonly RotatedRectangleTool _rotatedRectangleTool = new();
    private readonly CenterArcTool _centerArcTool = new();
    private readonly ThreePointArcTool _threePointArcTool = new();
    private readonly PieTool _pieTool = new();
    private readonly FlipTool _flipTool = new();
    private readonly AxesTool _axesTool = new();
    private readonly SectionPlaneTool _sectionTool = new();
    private readonly Text3DPlaceTool _text3DTool = new();

    private readonly List<SplitButton> _splitButtons = new();

    private void RegisterMoreCommands()
    {
        AddTool("tools/lasso", "Lasso", "Selects with a freehand or polygonal lasso. Counterclockwise = crossing, clockwise = fully inside.",
            Key.Space, ModifierKeys.Shift, "Icon.Lasso", _lassoTool);
        AddTool("draw/freehand", "Freehand", "Draws freehand curves by clicking and dragging.", Key.None, ModifierKeys.None, "Icon.Freehand", _freehandTool);
        AddTool("draw/rotated rectangle", "Rotated Rectangle", "Draws a rectangle at any angle.", Key.None, ModifierKeys.None,
            "Icon.RotatedRectangle", _rotatedRectangleTool);
        AddTool("draw/arc center", "Arc", "Draws an arc from a center point, a start point and an end point.", Key.None, ModifierKeys.None, "Icon.ArcCenter", _centerArcTool);
        AddTool("draw/arc 3 point", "3 Point Arc", "Draws an arc through three points.", Key.None, ModifierKeys.None, "Icon.Arc3Point", _threePointArcTool);
        AddTool("draw/pie", "Pie", "Draws a closed pie (face) from an arc and two radii.", Key.None, ModifierKeys.None, "Icon.Pie", _pieTool);
        AddTool("tools/flip", "Flip", "Flips the selection across a plane (Ctrl = flipped copy).", Key.None, ModifierKeys.None, "Icon.Flip", _flipTool);
        AddTool("tools/axes", "Axes", "Moves and rotates the drawing axes.", Key.None, ModifierKeys.None, "Icon.Axes", _axesTool);
        AddTool("tools/section plane", "Section Plane", "Cuts the model with a plane to show its interior.", Key.None, ModifierKeys.None,
            "Icon.SectionPlane", _sectionTool);
        Add(new AppCommand
        {
            Key = "tools/3d text", Title = "3D Text", Description = "Adds text as a group of faces (optionally extruded).",
            IconKey = "Icon.Text3D", Execute = Start3DText, IsChecked = () => ReferenceEquals(_tools.ActiveTool, _text3DTool),
        });
        Add(new AppCommand
        {
            Key = "edit/make component", Title = "Make Component...", ShortcutKey = Key.G, IconKey = "Icon.MakeComponent",
            Description = "Turns the selection into a named object (group).", Execute = MakeComponent,
        });
        Add(new AppCommand
        {
            Key = "app/search", Title = "Search", ShortcutKey = Key.S, ShortcutModifiers = ModifierKeys.Shift, IconKey = "Icon.Search",
            Description = "Finds and runs commands.", Execute = ShowSearch,
        });
        var axisNames = new[] { "Red", "Green", "Blue" };
        for (var i = 0; i < 3; i++)
        {
            var axis = i;
            Add(new AppCommand
            {
                Key = $"edit/flip {axisNames[i].ToLowerInvariant()}", Title = $"Flip Along {axisNames[i]} Direction",
                Description = "Flips the selection across a plane through its own center.",
                Execute = () => FlipTool.FlipSelection(Viewport, axis),
            });
        }
        Add(new AppCommand
        {
            Key = "edit/reset axes", Title = "Reset Axes", Description = "Returns the drawing axes to their original position.",
            Execute = () => Viewport.Model.Run("Reset Axes", () => Viewport.Model.Axes = ModelAxes.World),
        });
        Add(new AppCommand
        {
            Key = "view/section planes", Title = "Section Planes",
            Execute = () => SetStyle(Viewport.Model.Style with { HideSectionPlanes = !Viewport.Model.Style.HideSectionPlanes }),
            IsChecked = () => !Viewport.Model.Style.HideSectionPlanes,
        });
        Add(new AppCommand
        {
            Key = "view/section cuts", Title = "Section Cuts",
            Execute = () => SetStyle(Viewport.Model.Style with { HideSectionCuts = !Viewport.Model.Style.HideSectionCuts }),
            IsChecked = () => !Viewport.Model.Style.HideSectionCuts,
        });
        Add(new AppCommand
        {
            Key = "view/component edit/hide rest of model", Title = "Hide Rest of Model",
            Description = "Hides everything outside the group being edited (off: the rest is drawn faded).",
            Execute = () => Viewport.Model.HideRestOfModel = !Viewport.Model.HideRestOfModel,
            IsChecked = () => Viewport.Model.HideRestOfModel,
        });
        Add(new AppCommand
        {
            Key = "edit/reverse section", Title = "Reverse", Description = "Swaps the side of the model that the section plane shows.",
            Execute = () =>
            {
                var planes = Viewport.Selection.Items.OfType<SectionPlane>().ToList();
                if (planes.Count == 0) return;
                Viewport.Model.Run("Reverse Section", () => { foreach (var p in planes) p.Parent!.ReverseSection(p); });
            },
        });
        Add(new AppCommand
        {
            Key = "edit/active section", Title = "Active Cut", Description = "Turns cutting by the selected section plane on or off.",
            Execute = () =>
            {
                if (Viewport.Selection.Items.OfType<SectionPlane>().FirstOrDefault() is not { } p) return;
                Viewport.Model.Run("Active Cut", () => p.Parent!.SetActiveSection(p, !p.IsActive));
            },
            IsChecked = () => Viewport.Selection.Items.OfType<SectionPlane>().FirstOrDefault()?.IsActive == true,
        });

        _text3DTool.Placed += (_, _) => _tools.Select(_selectTool);

        Add(new AppCommand
        {
            Key = "extensions/deap design stage", Title = "DEAP Evidence Tool - Design Stage", IconKey = "Icon.Deap",
            Description = "Works out each dwelling's DEAP areas, height and openings from the tags and writes DEAP_Evidence_Output.csv.",
            Execute = () => RunDeap(null),
        });
        Add(new AppCommand
        {
            Key = "view/deap labels", Title = "DEAP Opening Labels",
            Description = "Shows each opening's id, orientation and area over the model after a DEAP run (nothing is added to the model).",
            Execute = () =>
            {
                _showDeapLabels = !_showDeapLabels;
                Viewport.Invalidate();
            },
            IsChecked = () => _showDeapLabels && _deapLabels is not null,
        });
    }

    /// <summary>The last DEAP results, drawn as screen labels over the openings (no groups or notes in the model).</summary>
    private List<Sketch.Operations.Deap.DeapDwelling>? _deapLabels;
    private bool _showDeapLabels = true;

    /// <summary>Drops the labels (a new or opened file has nothing to do with them).</summary>
    private void ClearDeapLabels()
    {
        _deapLabels = null;
        Viewport.ExtraOverlay = null;
    }

    /// <summary>
    /// Opening labels over the model. Every opening gets a dot; a label is written only where it does not overlap one
    /// already placed, so a distant view stays readable and zooming in reveals the rest.
    /// </summary>
    private void DrawDeapLabels(IViewDrawing g)
    {
        if (!_showDeapLabels || _deapLabels is null) return;
        var red = new DrawColor(200, 0, 0);
        var cam = Viewport.Camera;
        int w = Viewport.PixelWidth, h = Viewport.PixelHeight;
        var placed = new List<(double X0, double Y0, double X1, double Y1)>();
        const double charPx = 7.2, labelHeight = 22, lift = 16;
        foreach (var d in _deapLabels)
        {
            foreach (var o in d.Openings)
            {
                if (o.Face.IsDeleted) continue;
                var centre = Vector3d.Zero;
                var n = 0;
                foreach (var v in o.Face.Vertices)
                {
                    centre += v.Position;
                    n++;
                }
                if (n == 0) continue;
                centre /= n;
                g.Point(centre, red, 6, PointShape.Circle);
                if (cam.WorldToScreen(centre, w, h) is not { } s) continue;
                var text = $"{o.Id}  {o.Orientation}  {Sketch.Operations.Deap.DeapEvidence.Fmt(o.AreaSqm)} m²";
                var half = text.Length * charPx / 2 + 6;
                var box = (X0: s.X - half, Y0: s.Y - lift - labelHeight / 2, X1: s.X + half, Y1: s.Y - lift + labelHeight / 2);
                if (placed.Any(p => box.X0 < p.X1 && box.X1 > p.X0 && box.Y0 < p.Y1 && box.Y1 > p.Y0)) continue;
                placed.Add(box);
                g.Text(centre, text, red, 0, -lift);
            }
        }
    }

    /// <summary>
    /// DEAP Evidence Tool (design stage): CSV for the selected (or all) dwellings. The openings' ids, orientations
    /// and areas are then shown as labels over the model (View > DEAP Opening Labels); QA label groups left in the
    /// model by the SketchUp extension are removed, so the model itself stays clean.
    /// </summary>
    private void RunDeap(string? folder)
    {
        _tools.CancelActive();
        var model = Viewport.Model;
        if (folder is null)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose DEAP design-stage CSV output folder" };
            if (dialog.ShowDialog(this) != true) return;
            folder = dialog.FolderName;
        }
        var dwellings = Sketch.Operations.Deap.DeapEvidence.FindDwellings(model, Viewport.Selection.Items);
        if (dwellings.Count == 0)
        {
            ShowMessage("No dwelling groups found. Select dwelling groups or ensure each dwelling is a top-level group with floor tags.");
            return;
        }
        var north = Sketch.Operations.Deap.DeapEvidence.NorthVector(model);
        var results = dwellings.Select((d, i) => Sketch.Operations.Deap.DeapEvidence.Analyse(d, north, i + 1)).ToList();
        var path = System.IO.Path.Combine(folder, Sketch.Operations.Deap.DeapEvidence.CsvFileName);
        try
        {
            System.IO.File.WriteAllText(path, Sketch.Operations.Deap.DeapEvidence.ToCsv(results), new System.Text.UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            ShowMessage($"Could not write the CSV file:\n{ex.Message}");
            return;
        }
        var oldQa = model.Entities.Groups
            .Where(g => g.Name.StartsWith(Sketch.Operations.Deap.DeapEvidence.QaGroupPrefix, StringComparison.Ordinal))
            .Cast<Entity>().ToList();
        if (oldQa.Count > 0) model.Run("Remove DEAP QA label groups", () => model.Entities.Erase(oldQa));
        LastDeapResults = results;
        _deapLabels = results;
        _showDeapLabels = true;
        Viewport.ExtraOverlay = DrawDeapLabels;
        Viewport.Invalidate();
        RefreshToolState();
        ShowMessage($"DEAP design-stage CSV export complete.\n\nDwellings processed: {results.Count}\nCSV folder: {folder}\n\n" +
                    "Each opening's id, orientation and area are shown over the model (View > DEAP Opening Labels turns them off). " +
                    (oldQa.Count > 0 ? $"{oldQa.Count} QA label groups from the SketchUp extension were removed from the model." : "Nothing was added to the model."));
    }

    /// <summary>Results of the last DEAP run (used by test mode).</summary>
    internal List<Sketch.Operations.Deap.DeapDwelling>? LastDeapResults { get; private set; }

    private void ShowMessage(string text)
    {
        if (Application.Current.Properties.Contains("Script")) _scriptLog?.WriteLine("mesaj: " + text.Replace("\n", " | "));
        else MessageBox.Show(this, text, "DEAP Evidence Tool - Design Stage", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Start3DText()
    {
        _tools.CancelActive();
        if (Panels.Text3DDialog.Show(this) is not { } result) return;
        _text3DTool.Prepare(result.Loops, result.Filled, result.Extrude, result.Text.Split('\n')[0].Trim());
        _tools.Select(_text3DTool);
        FocusSinkFocus();
    }

    private void MakeComponent()
    {
        var model = Viewport.Model;
        var items = Viewport.Selection.Items.Where(e => !e.IsDeleted && ReferenceEquals(e.Parent, model.ActiveEntities)).ToList();
        if (items.Count == 0) return;
        _tools.CancelActive();
        var count = model.Entities.AllGroups().Count() + 1;
        var name = Application.Current.Properties.Contains("Script")
            ? $"Component#{count}"
            : Panels.Prompt.Show(this, "Component name:", "Make Component", $"Component#{count}");
        if (name is null) return;
        Group? group = null;
        model.Run("Make Component", () => group = model.ActiveEntities.MakeGroup(items, name));
        if (group is not null) Viewport.Selection.Set(new[] { group });
    }

    // ---------------- Toolbars ----------------

    /// <summary>A toolbar button with a ▾ menu; the button shows (and runs) the last tool chosen from it.</summary>
    private sealed class SplitButton
    {
        public required ToggleButton Main { get; init; }
        public required Image Icon { get; init; }
        public required List<AppCommand> Commands { get; init; }
        public AppCommand Current { get; set; } = null!;
    }

    private FrameworkElement SplitButtonFor(params string[] keys)
    {
        var commands = keys.Select(k => _byKey[k]).ToList();
        var icon = new Image();
        var main = new ToggleButton { Style = (Style)FindResource("ToolButton"), Content = icon, Margin = new Thickness(1, 1, 0, 1) };
        var split = new SplitButton { Main = main, Icon = icon, Commands = commands };
        SetSplitCurrent(split, commands[0]);
        main.Click += (_, _) => Run(split.Current);

        var arrow = new Button
        {
            Content = new TextBlock { Text = "▾", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(80, 86, 96)) },
            Width = 11, Height = 33, Padding = new Thickness(0), Margin = new Thickness(0, 1, 1, 1), Focusable = false,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), ToolTip = "More tools",
        };
        arrow.Click += (_, _) =>
        {
            var menu = new ContextMenu { PlacementTarget = main, Placement = PlacementMode.Bottom };
            foreach (var c in commands)
            {
                var item = new MenuItem { Header = c.Title, InputGestureText = c.ShortcutText, IsChecked = c.IsChecked?.Invoke() == true };
                if (c.IconKey is not null && TryFindResource(c.IconKey) is ImageSource img) item.Icon = new Image { Source = img, Width = 16, Height = 16 };
                var cmd = c;
                item.Click += (_, _) =>
                {
                    SetSplitCurrent(split, cmd);
                    Run(cmd);
                };
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        };
        _splitButtons.Add(split);
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(main);
        panel.Children.Add(arrow);
        return panel;
    }

    private void SetSplitCurrent(SplitButton split, AppCommand command)
    {
        split.Current = command;
        split.Icon.Source = (ImageSource)FindResource(command.IconKey!);
        split.Main.ToolTip = command.ToolTipText;
    }

    /// <summary>Split buttons follow the active tool (e.g. pressing Shift+Space shows the Lasso on the Select button).</summary>
    private void RefreshSplitButtons()
    {
        foreach (var split in _splitButtons)
        {
            var active = split.Commands.FirstOrDefault(c => c.IsChecked?.Invoke() == true);
            if (active is not null && !ReferenceEquals(active, split.Current)) SetSplitCurrent(split, active);
            split.Main.IsChecked = active is not null;
        }
    }

    private void BuildToolbars()
    {
        void Sep(Panel panel) => panel.Children.Add(panel == TopToolbar
            ? new Border { Width = 1, Margin = new Thickness(3, 4, 3, 4), Background = Brushes.Silver }
            : new Border { Height = 1, Width = 68, Margin = new Thickness(2, 4, 2, 4), Background = Brushes.Silver });
        void Buttons(Panel panel, params string[] keys)
        {
            foreach (var k in keys) panel.Children.Add(ButtonFor(_byKey[k]));
        }

        // Top: like SketchUp's "Getting Started" toolbar, followed by the Views toolbar.
        Buttons(TopToolbar, "app/search");
        Sep(TopToolbar);
        TopToolbar.Children.Add(SplitButtonFor("tools/select", "tools/lasso"));
        Sep(TopToolbar);
        Buttons(TopToolbar, "tools/eraser");
        TopToolbar.Children.Add(SplitButtonFor("draw/line", "draw/freehand"));
        TopToolbar.Children.Add(SplitButtonFor("draw/arc center", "draw/arc", "draw/arc 3 point", "draw/pie"));
        TopToolbar.Children.Add(SplitButtonFor("draw/rectangle", "draw/rotated rectangle", "draw/circle", "draw/polygon"));
        Sep(TopToolbar);
        Buttons(TopToolbar, "tools/pushpull", "tools/offset", "tools/move", "tools/rotate", "tools/scale", "tools/flip");
        Sep(TopToolbar);
        Buttons(TopToolbar, "tools/tape", "tools/paint");
        Sep(TopToolbar);
        Buttons(TopToolbar, "camera/orbit", "camera/pan", "camera/zoom", "camera/zoom extents");
        Sep(TopToolbar);
        Buttons(TopToolbar, "camera/standard views/iso", "camera/standard views/top", "camera/standard views/front",
            "camera/standard views/right", "camera/standard views/left", "camera/standard views/back", "camera/standard views/bottom");
        Sep(TopToolbar);
        var deap = ButtonFor(_byKey["extensions/deap design stage"]);
        deap.ToolTip = "DEAP Evidence Tool - Design Stage\nGenerates the design-stage CSV (areas, height, openings per dwelling) and shows the opening labels over the model.";
        TopToolbar.Children.Add(deap);

        // Left: like SketchUp's "Large Tool Set" (two columns).
        Buttons(LeftToolbar, "tools/select", "tools/lasso", "tools/paint", "tools/eraser", "edit/make component", "tools/tag");
        Sep(LeftToolbar);
        Buttons(LeftToolbar, "draw/line", "draw/freehand", "draw/rectangle", "draw/rotated rectangle", "draw/circle", "draw/polygon",
            "draw/arc center", "draw/arc", "draw/arc 3 point", "draw/pie");
        Sep(LeftToolbar);
        Buttons(LeftToolbar, "tools/move", "tools/pushpull", "tools/rotate", "tools/followme", "tools/scale", "tools/offset",
            "tools/axes", "tools/flip");
        Sep(LeftToolbar);
        Buttons(LeftToolbar, "tools/tape", "tools/dimension", "tools/protractor", "tools/text", "tools/section plane", "tools/3d text");
        Sep(LeftToolbar);
        Buttons(LeftToolbar, "camera/orbit", "camera/pan", "camera/zoom", "camera/zoom window", "camera/zoom extents", "camera/previous");
    }

    // ---------------- Search (Shift+S) ----------------

    private Popup? _searchPopup;

    private void ShowSearch()
    {
        _tools.CancelActive();
        var box = new TextBox { Width = 300, Margin = new Thickness(6, 6, 6, 4), Padding = new Thickness(3, 2, 3, 2) };
        var list = new ListBox { Width = 300, MaxHeight = 320, Margin = new Thickness(6, 0, 6, 6), BorderThickness = new Thickness(0) };
        var panel = new StackPanel { Background = Brushes.White };
        panel.Children.Add(box);
        panel.Children.Add(list);
        var popup = new Popup
        {
            Child = new Border { Child = panel, BorderBrush = new SolidColorBrush(Color.FromRgb(160, 168, 180)), BorderThickness = new Thickness(1) },
            PlacementTarget = TopToolbar, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = false,
        };
        _searchPopup = popup;

        var searchable = _commands.Where(c => c.Key != "app/search").ToList();
        void Fill()
        {
            var q = box.Text.Trim();
            var tr = new System.Globalization.CultureInfo("tr-TR");
            var hits = searchable
                .Where(c => q.Length == 0
                            || tr.CompareInfo.IndexOf(c.Title, q, System.Globalization.CompareOptions.IgnoreCase) >= 0
                            || (c.Description is { } d && tr.CompareInfo.IndexOf(d, q, System.Globalization.CompareOptions.IgnoreCase) >= 0))
                .OrderBy(c => tr.CompareInfo.IndexOf(c.Title, q, System.Globalization.CompareOptions.IgnoreCase) < 0)
                .ThenBy(c => c.Title, StringComparer.Create(tr, true))
                .Take(14)
                .ToList();
            list.Items.Clear();
            foreach (var c in hits)
            {
                var row = new DockPanel { Tag = c, Margin = new Thickness(2) };
                var shortcut = new TextBlock { Text = c.ShortcutText, Foreground = Brushes.Gray, Margin = new Thickness(12, 0, 0, 0) };
                DockPanel.SetDock(shortcut, Dock.Right);
                row.Children.Add(shortcut);
                if (c.IconKey is not null && TryFindResource(c.IconKey) is ImageSource img)
                    row.Children.Add(new Image { Source = img, Width = 18, Height = 18, Margin = new Thickness(0, 0, 6, 0) });
                row.Children.Add(new TextBlock { Text = c.Title, VerticalAlignment = VerticalAlignment.Center });
                list.Items.Add(row);
            }
            list.SelectedIndex = list.Items.Count > 0 ? 0 : -1;
        }
        void RunSelected()
        {
            if (list.SelectedItem is not DockPanel { Tag: AppCommand cmd }) return;
            popup.IsOpen = false;
            FocusSinkFocus();
            Run(cmd);
        }
        box.TextChanged += (_, _) => Fill();
        box.PreviewKeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Down when list.Items.Count > 0:
                    list.SelectedIndex = Math.Min(list.SelectedIndex + 1, list.Items.Count - 1);
                    e.Handled = true;
                    break;
                case Key.Up when list.Items.Count > 0:
                    list.SelectedIndex = Math.Max(list.SelectedIndex - 1, 0);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    RunSelected();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    popup.IsOpen = false;
                    FocusSinkFocus();
                    e.Handled = true;
                    break;
            }
        };
        list.MouseLeftButtonUp += (_, _) => RunSelected();
        popup.Closed += (_, _) => _searchPopup = null;
        Fill();
        popup.IsOpen = true;
        box.Focus();
        Keyboard.Focus(box);
    }
}
