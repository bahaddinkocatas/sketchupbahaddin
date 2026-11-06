using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Sketch.App.Commands;
using Sketch.App.Panels;
using Sketch.Model;
using Sketch.Tools;
using Sketch.Tools.Drawing;
using Sketch.Tools.Modify;
using Sketch.Tools.Navigation;

namespace Sketch.App;

public partial class MainWindow : Window
{
    private readonly ToolManager _tools;
    private readonly SelectTool _selectTool = new();
    private readonly LineTool _lineTool = new();
    private readonly RectangleTool _rectangleTool = new();
    private readonly CircleTool _circleTool = new();
    private readonly PushPullTool _pushPullTool = new();
    private readonly MoveTool _moveTool = new();
    private readonly EraserTool _eraserTool = new();
    private readonly OrbitTool _orbitTool = new();
    private readonly PanTool _panTool = new();
    private readonly ZoomTool _zoomTool = new();
    private readonly PaintBucketTool _paintTool = new();
    private readonly TagTool _tagTool = new();
    private readonly ArcTool _arcTool = new();
    private readonly PolygonTool _polygonTool = new();
    private readonly OffsetTool _offsetTool = new();
    private readonly RotateTool _rotateTool = new();
    private readonly ScaleTool _scaleTool = new();
    private readonly FollowMeTool _followMeTool = new();
    private readonly Sketch.Tools.Construction.TapeMeasureTool _tapeTool = new();
    private readonly Sketch.Tools.Construction.ProtractorTool _protractorTool = new();
    private readonly ZoomWindowTool _zoomWindowTool = new();
    private readonly Sketch.Tools.Construction.DimensionTool _dimensionTool = new();
    private readonly Sketch.Tools.Construction.TextTool _textTool = new();
    private readonly ImagePlaceTool _imageTool = new();
    private EntityInfoPanel _entityInfo = null!;
    private ScenesBar _scenes = null!;
    private bool _updatingTagCombo;

    private readonly List<AppCommand> _commands = new();
    private readonly Dictionary<string, AppCommand> _byKey = new();
    private readonly List<(ToggleButton Button, AppCommand Command)> _toggleButtons = new();
    private readonly List<(MenuItem Item, AppCommand Command)> _checkableItems = new();
    private readonly FileController _files;

    public MainWindow()
    {
        InitializeComponent();
        var scripted = Application.Current.Properties.Contains("Script");
        if (scripted)
        {
            // Test mode must not take focus from whatever the user is doing.
            ShowActivated = false;
            WindowState = WindowState.Normal; // fixed size so test screenshots are comparable
            Topmost = false;
        }

        _tools = new ToolManager(Viewport, _selectTool);
        Viewport.Tools = _tools;
        _zoomWindowTool.Finished += (_, _) => _tools.SelectPrevious();
        _imageTool.Placed += (_, _) => _tools.Select(_selectTool);
        _tapeTool.ConfirmResize = (current, wanted) => Application.Current.Properties.Contains("Script")
            || MessageBox.Show(this,
                $"The measured length is {Sketch.Geometry.Units.Default.Format(current)}. Do you want to resize the model so this length becomes {Sketch.Geometry.Units.Default.Format(wanted)}?",
                "Resize Model", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        _textTool.AskText = initial => Application.Current.Properties.Contains("Script")
            ? initial
            : Panels.Prompt.Show(this, "Text:", "Text", initial);
        _tools.StateChanged += (_, _) => RefreshToolState();
        Viewport.ViewMouseDown += (_, _) => FocusViewport();
        Viewport.KeyForwarded += (vk, down) =>
        {
            var key = KeyInterop.KeyFromVirtualKey(vk);
            if (down) ProcessKey(key, Keyboard.Modifiers);
            else _tools.KeyUp(ToViewKey(key), ToToolModifiers(Keyboard.Modifiers));
        };
        Viewport.CharForwarded += c =>
        {
            if (char.IsDigit(c) || "-.,;[]<>*/'\"".Contains(c)) StartMeasurement(c.ToString());
        };
        if (!scripted) Viewport.TooltipRequested += ShowCursorTip;
        Viewport.Model.HistoryChanged += (_, _) => RefreshTitle();
        _files = new FileController(this, Viewport.Model, Viewport.Camera, () => _tools.CancelActive());
        _files.FileChanged += (_, _) =>
        {
            Viewport.Selection.Clear();
            ClearDeapLabels();
            if (Viewport.Model.Entities.BoundingSphere() is not null) NavigationMath.ZoomExtents(Viewport);
            Viewport.Invalidate();
            RefreshTitle();
        };

        // A new document starts from the thermal-modelling template (tags, Glass/Door, scenes).
        FileController.ResetToTemplate(Viewport.Model);
        Viewport.Model.ResetHistory();

        RegisterCommands();
        BuildMenu();
        BuildToolbars();
        BuildTray();
        SetupTagCombo();
        Viewport.ContextMenuRequested += ShowViewContextMenu;
        Viewport.Model.EditContextChanged += (_, _) => RefreshToolState();

        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
        PreviewTextInput += OnPreviewTextInput;
        MeasurementsBox.KeyDown += OnMeasurementsKeyDown;
        Deactivated += (_, _) => CursorTip.IsOpen = false;
        Closing += (_, e) => e.Cancel = !_files.ConfirmDiscardChanges();
        Loaded += (_, _) =>
        {
            FocusViewport();
            RefreshToolState();
            RefreshTitle();
            if (Application.Current.Properties["OpenFile"] is string path) _files.Open(path);
            if (Application.Current.Properties["Script"] is string script) RunScript(script);
        };
    }

    // ---------------- Commands ----------------

    private void RegisterCommands()
    {
        AddTool("tools/select", "Select", "Selects entities.", Key.Space, ModifierKeys.None, "Icon.Select", _selectTool);
        AddTool("tools/eraser", "Eraser", "Erases, hides or softens edges.", Key.E, ModifierKeys.None, "Icon.Eraser", _eraserTool);
        AddTool("draw/line", "Line", "Draws edges.", Key.L, ModifierKeys.None, "Icon.Line", _lineTool);
        AddTool("draw/rectangle", "Rectangle", "Draws rectangular faces.", Key.R, ModifierKeys.None, "Icon.Rectangle", _rectangleTool);
        AddTool("draw/circle", "Circle", "Draws circles.", Key.C, ModifierKeys.None, "Icon.Circle", _circleTool);
        AddTool("tools/pushpull", "Push/Pull", "Pushes or pulls faces to add or remove volume.", Key.P, ModifierKeys.None, "Icon.PushPull", _pushPullTool);
        AddTool("tools/move", "Move", "Moves or copies (Ctrl) entities.", Key.M, ModifierKeys.None, "Icon.Move", _moveTool);
        AddTool("camera/orbit", "Orbit", "Orbits the camera around the model.", Key.O, ModifierKeys.None, "Icon.Orbit", _orbitTool);
        AddTool("camera/pan", "Pan", "Pans the view horizontally or vertically.", Key.H, ModifierKeys.None, "Icon.Pan", _panTool);
        AddTool("camera/zoom", "Zoom", "Zooms the view in or out.", Key.Z, ModifierKeys.None, "Icon.Zoom", _zoomTool);

        Add(new AppCommand
        {
            Key = "camera/zoom extents", Title = "Zoom Extents", Description = "Zooms out to fit the entire model in the view.",
            ShortcutKey = Key.Z, ShortcutModifiers = ModifierKeys.Shift, IconKey = "Icon.ZoomExtents",
            Execute = () => NavigationMath.ZoomExtents(Viewport),
        });

        AddView("camera/standard views/iso", "Iso", StandardView.Iso, "Icon.ViewIso");
        AddView("camera/standard views/top", "Top", StandardView.Top, "Icon.ViewTop");
        AddView("camera/standard views/bottom", "Bottom", StandardView.Bottom, "Icon.ViewBottom");
        AddView("camera/standard views/front", "Front", StandardView.Front, "Icon.ViewFront");
        AddView("camera/standard views/back", "Back", StandardView.Back, "Icon.ViewBack");
        AddView("camera/standard views/left", "Left", StandardView.Left, "Icon.ViewLeft");
        AddView("camera/standard views/right", "Right", StandardView.Right, "Icon.ViewRight");

        Add(new AppCommand
        {
            Key = "edit/undo", Title = "Undo", ShortcutKey = Key.Z, ShortcutModifiers = ModifierKeys.Control, IconKey = "Icon.Undo",
            Execute = () =>
            {
                _tools.CancelActive();
                Viewport.Model.Undo();
            },
        });
        Add(new AppCommand
        {
            Key = "edit/redo", Title = "Redo", ShortcutKey = Key.Y, ShortcutModifiers = ModifierKeys.Control, IconKey = "Icon.Redo",
            Execute = () =>
            {
                _tools.CancelActive();
                Viewport.Model.Redo();
            },
        });
        Add(new AppCommand
        {
            Key = "edit/delete", Title = "Delete", ShortcutKey = Key.Delete, Description = "Deletes the selected entities.",
            Execute = DeleteSelection,
        });
        Add(new AppCommand
        {
            Key = "edit/select all", Title = "Select All", ShortcutKey = Key.A, ShortcutModifiers = ModifierKeys.Control,
            Execute = () =>
            {
                var c = Viewport.Model.ActiveEntities;
                Viewport.Selection.Set(c.Edges.Cast<Entity>().Concat(c.Faces).Concat(c.Groups).Concat(c.Images).Concat(c.Notes)
                    .Concat(c.Dimensions).Concat(c.Guides).Concat(c.Sections).Where(Entities.IsEffectivelyVisible));
            },
        });
        Add(new AppCommand
        {
            Key = "edit/select none", Title = "Select None", ShortcutKey = Key.T, ShortcutModifiers = ModifierKeys.Control,
            Execute = () => Viewport.Selection.Clear(),
        });
        Add(new AppCommand
        {
            Key = "edit/reverse faces", Title = "Reverse Faces", Description = "Swaps the front and back sides of the selected faces.",
            Execute = () =>
            {
                var faces = Viewport.Selection.Items.OfType<Face>().ToList();
                if (faces.Count == 0) return;
                Viewport.Model.Run("Reverse Faces", () =>
                {
                    foreach (var f in faces) Viewport.Model.Entities.Reverse(f);
                });
            },
        });

        AddTool("tools/paint", "Paint Bucket", "Applies materials to faces.", Key.B, ModifierKeys.None, "Icon.Paint", _paintTool);
        AddTool("draw/arc", "2 Point Arc", "Draws an arc from two end points and a bulge.", Key.A, ModifierKeys.None, "Icon.Arc", _arcTool);
        AddTool("draw/polygon", "Polygon", "Draws regular polygons.", Key.None, ModifierKeys.None, "Icon.Polygon", _polygonTool);
        AddTool("tools/offset", "Offset", "Draws an inward or outward copy of a face's boundary.", Key.F, ModifierKeys.None, "Icon.Offset", _offsetTool);
        AddTool("tools/rotate", "Rotate", "Rotates the selection around an axis (Ctrl = copy).", Key.Q, ModifierKeys.None, "Icon.Rotate", _rotateTool);
        AddTool("tools/scale", "Scale", "Scales the selection with grips.", Key.S, ModifierKeys.None, "Icon.Scale", _scaleTool);
        AddTool("tools/followme", "Follow Me", "Sweeps a profile along the selected path.", Key.None, ModifierKeys.None, "Icon.FollowMe", _followMeTool);
        AddTool("tools/tape", "Tape Measure", "Measures distances and creates guides.", Key.T, ModifierKeys.None, "Icon.Tape", _tapeTool);
        AddTool("tools/dimension", "Dimensions", "Places a dimension between two points.", Key.None, ModifierKeys.None, "Icon.Dimension", _dimensionTool);
        AddTool("tools/text", "Text", "Adds text labels to entities.", Key.None, ModifierKeys.None, "Icon.Text", _textTool);
        AddTool("tools/protractor", "Protractor", "Creates angled guides.", Key.None, ModifierKeys.None, "Icon.Protractor", _protractorTool);
        AddTool("camera/zoom window", "Zoom Window", "Zooms in on a selected area.", Key.None, ModifierKeys.None, "Icon.ZoomWindow", _zoomWindowTool);
        Add(new AppCommand
        {
            Key = "camera/previous", Title = "Previous", IconKey = "Icon.Previous", Description = "Returns to the previous camera position.",
            Execute = () => Viewport.PreviousCamera(),
        });
        Add(new AppCommand
        {
            Key = "edit/delete guides", Title = "Delete Guides", Description = "Deletes all guide lines and guide points.",
            Execute = () => Viewport.Model.Run("Delete Guides", () => Viewport.Model.ActiveEntities.DeleteAllGuides()),
        });
        AddTool("tools/tag", "Tag", "Applies the active tag to the clicked entity.", Key.None, ModifierKeys.None, "Icon.TagTool", _tagTool);

        Add(new AppCommand
        {
            Key = "edit/make group", Title = "Make Group", IconKey = "Icon.Group",
            Description = "Groups the selected entities.", Execute = MakeGroup,
        });
        Add(new AppCommand { Key = "edit/explode", Title = "Explode", Description = "Explodes the selected groups.", Execute = ExplodeSelection });
        Add(new AppCommand { Key = "edit/open group", Title = "Edit Group", Description = "Opens the selected group for editing.", Execute = OpenSelectedGroup });
        Add(new AppCommand { Key = "edit/close group", Title = "Close Group", Description = "Closes the group being edited.", Execute = () => Viewport.Model.ExitGroup() });
        Add(new AppCommand { Key = "edit/hide", Title = "Hide", ShortcutKey = Key.H, ShortcutModifiers = ModifierKeys.Control, Description = "Hides the selection (or, with no selection, the entity under the cursor).", Execute = () => SetHidden(true) });
        Add(new AppCommand { Key = "edit/unhide all", Title = "Unhide All", ShortcutKey = Key.H, ShortcutModifiers = ModifierKeys.Control | ModifierKeys.Shift, Execute = UnhideAll });

        AddStyle("view/style/wireframe", "Wireframe", FaceStyle.Wireframe);
        AddStyle("view/style/hidden line", "Hidden Line", FaceStyle.HiddenLine);
        AddStyle("view/style/shaded", "Shaded", FaceStyle.Shaded);
        AddStyle("view/style/textured", "Shaded With Textures", FaceStyle.ShadedWithTextures);
        AddStyle("view/style/monochrome", "Monochrome", FaceStyle.Monochrome);
        Add(new AppCommand
        {
            Key = "view/xray", Title = "X-ray", IconKey = "Icon.XRay",
            Execute = () => SetStyle(Viewport.Model.Style with { XRay = !Viewport.Model.Style.XRay }),
            IsChecked = () => Viewport.Model.Style.XRay,
        });
        Add(new AppCommand
        {
            Key = "view/back edges", Title = "Back Edges", ShortcutKey = Key.K, IconKey = "Icon.BackEdges",
            Execute = () => SetStyle(Viewport.Model.Style with { BackEdges = !Viewport.Model.Style.BackEdges }),
            IsChecked = () => Viewport.Model.Style.BackEdges,
        });
        Add(new AppCommand
        {
            Key = "view/color by tag", Title = "Color by Tag", IconKey = "Icon.ColorByTag",
            Execute = () => SetStyle(Viewport.Model.Style with { ColorByTag = !Viewport.Model.Style.ColorByTag }),
            IsChecked = () => Viewport.Model.Style.ColorByTag,
        });
        Add(new AppCommand
        {
            Key = "view/edge color", Title = "Edge Color...", Description = "Changes the color of edges.",
            Execute = () =>
            {
                var current = Viewport.Model.Style.EdgeColor ?? new Rgb(230, 110, 15);
                if (Panels.ColorPicker.Show(this, current) is { } c) SetStyle(Viewport.Model.Style with { EdgeColor = c });
            },
        });
        Add(new AppCommand
        {
            Key = "view/background", Title = "Background Color...", Description = "Changes the background color of the 3D view.",
            Execute = () =>
            {
                var current = Viewport.Model.Style.Background ?? new Rgb(211, 208, 186);
                if (Panels.ColorPicker.Show(this, current) is { } c) SetStyle(Viewport.Model.Style with { Background = c });
            },
        });

        Add(new AppCommand { Key = "file/new", Title = "New", ShortcutKey = Key.N, ShortcutModifiers = ModifierKeys.Control, IconKey = "Icon.New", Execute = () => _files.New() });
        Add(new AppCommand { Key = "file/open", Title = "Open...", ShortcutKey = Key.O, ShortcutModifiers = ModifierKeys.Control, IconKey = "Icon.Open", Execute = () => _files.OpenWithDialog() });
        Add(new AppCommand { Key = "file/save", Title = "Save", ShortcutKey = Key.S, ShortcutModifiers = ModifierKeys.Control, IconKey = "Icon.Save", Execute = () => _files.Save() });
        Add(new AppCommand { Key = "file/save as", Title = "Save As...", ShortcutKey = Key.S, ShortcutModifiers = ModifierKeys.Control | ModifierKeys.Shift, Execute = () => _files.SaveAs() });
        Add(new AppCommand { Key = "file/import image", Title = "Import Image...", ShortcutKey = Key.I, ShortcutModifiers = ModifierKeys.Control, IconKey = "Icon.Image", Description = "Places an image such as a PNG/JPG (e.g. a floor plan) in the model.", Execute = () => ImportImageWithDialog() });
        Add(new AppCommand { Key = "file/import skp", Title = "Import SketchUp Model (.skp)...", Description = "Adds the contents of a SketchUp file to the current model.", Execute = () => _files.ImportSkpWithDialog() });
        Add(new AppCommand
        {
            Key = "file/export png", Title = "Export 2D Image (PNG)...",
            Description = "Saves the current view as a PNG picture.", Execute = ExportPngWithDialog,
        });
        Add(new AppCommand { Key = "file/exit", Title = "Exit", Execute = Close });
        Add(new AppCommand
        {
            Key = "window/default tray", Title = "Default Tray", Description = "Shows or hides the panels on the right.",
            Execute = () =>
            {
                var hidden = TrayColumn.Width.Value > 0;
                TrayColumn.Width = hidden ? new GridLength(0) : new GridLength(330);
                TrayColumn.MinWidth = hidden ? 0 : 220;
                SplitterColumn.Width = new GridLength(hidden ? 0 : 5);
            },
            IsChecked = () => TrayColumn.Width.Value > 0,
        });
        Add(new AppCommand { Key = "help/about", Title = "About Sketch", Execute = ShowAbout });
        Add(new AppCommand { Key = "help/shortcuts", Title = "Keyboard Shortcuts", Execute = ShowShortcuts });
        RegisterMoreCommands();
    }

    private void Add(AppCommand command)
    {
        _commands.Add(command);
        _byKey[command.Key] = command;
    }

    private void AddTool(string key, string title, string description, Key shortcut, ModifierKeys modifiers, string icon, Tool tool) =>
        Add(new AppCommand
        {
            Key = key, Title = title, Description = description, ShortcutKey = shortcut, ShortcutModifiers = modifiers,
            IconKey = icon, Execute = () => _tools.Select(tool), IsChecked = () => ReferenceEquals(_tools.ActiveTool, tool),
        });

    private void AddView(string key, string title, StandardView view, string icon) =>
        Add(new AppCommand
        {
            Key = key, Title = title, IconKey = icon, Description = $"{title} view.",
            Execute = () =>
            {
                var (center, radius) = Viewport.ModelBounds;
                Viewport.Camera.SetStandardView(view, center, radius);
                Viewport.Invalidate();
            },
        });

    /// <summary>File &gt; Export 2D Image: the view as it is drawn, saved as a PNG.</summary>
    private void ExportPngWithDialog()
    {
        _tools.CancelActive();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG image (*.png)|*.png", DefaultExt = "png",
            FileName = (_files.CurrentPath is { } p ? System.IO.Path.GetFileNameWithoutExtension(p) : _files.SuggestedName ?? "Untitled") + ".png",
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            Viewport.SaveImage(dialog.FileName);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, "Could not save the image: " + ex.Message, "Sketch", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DeleteSelection()
    {
        var items = Viewport.Selection.Items.ToList();
        if (items.Count == 0) return;
        if (!ReferenceEquals(_tools.ActiveTool, _selectTool)) _tools.CancelActive();
        Viewport.Model.Run("Erase", () => Viewport.Model.ActiveEntities.Erase(items));
        Viewport.Selection.Clear();
    }

    private void Run(AppCommand command)
    {
        command.Execute();
        RefreshToolState();
    }

    // ---------------- Menu & toolbars ----------------

    private void BuildMenu()
    {
        var file = MenuOf("_File", "file/new", "file/open", null, "file/save", "file/save as", null, "file/import image", "file/import skp", null, "file/export png", null, "file/exit");
        var recent = new MenuItem { Header = "Open _Recent" };
        file.Items.Insert(2, recent);
        file.SubmenuOpened += (_, _) => FillRecent(recent);
        MainMenu.Items.Add(file);
        MainMenu.Items.Add(MenuOf("_Edit", "edit/undo", "edit/redo", null, "edit/delete", null, "edit/select all", "edit/select none",
            null, "edit/hide", "edit/unhide all", null, "edit/make group", "edit/make component", "edit/open group", "edit/close group", "edit/explode",
            null, "edit/flip red", "edit/flip green", "edit/flip blue", null, "edit/reverse faces", "edit/delete guides"));

        var view = MenuOf("_View");
        view.Items.Clear();
        var faceStyle = new MenuItem { Header = "Face Style" };
        foreach (var k in new[] { "xray", null, "style/wireframe", "style/hidden line", "style/shaded", "style/textured", "style/monochrome" })
        {
            if (k is null) faceStyle.Items.Add(new Separator());
            else faceStyle.Items.Add(ItemFor(_byKey["view/" + k]));
        }
        view.Items.Add(faceStyle);
        view.Items.Add(ItemFor(_byKey["view/back edges"]));
        view.Items.Add(ItemFor(_byKey["view/color by tag"]));
        view.Items.Add(new Separator());
        view.Items.Add(ItemFor(_byKey["view/section planes"]));
        view.Items.Add(ItemFor(_byKey["view/section cuts"]));
        view.Items.Add(ItemFor(_byKey["view/deap labels"]));
        view.Items.Add(new Separator());
        var componentEdit = new MenuItem { Header = "Component Edit" };
        componentEdit.Items.Add(ItemFor(_byKey["view/component edit/hide rest of model"]));
        view.Items.Add(componentEdit);
        view.Items.Add(new Separator());
        view.Items.Add(ItemFor(_byKey["view/edge color"]));
        view.Items.Add(ItemFor(_byKey["view/background"]));
        MainMenu.Items.Add(view);

        var camera = MenuOf("_Camera");
        var views = new MenuItem { Header = "Standard Views" };
        foreach (var k in new[] { "top", "bottom", "front", "back", "left", "right", "iso" })
            views.Items.Add(ItemFor(_byKey["camera/standard views/" + k]));
        camera.Items.Clear();
        camera.Items.Add(views);
        camera.Items.Add(new Separator());
        camera.Items.Insert(0, ItemFor(_byKey["camera/previous"]));
        camera.Items.Insert(1, new Separator());
        foreach (var k in new[] { "camera/orbit", "camera/pan", "camera/zoom", "camera/zoom window", "camera/zoom extents" })
            camera.Items.Add(ItemFor(_byKey[k]));
        MainMenu.Items.Add(camera);

        MainMenu.Items.Add(MenuOf("_Draw", "draw/line", "draw/freehand", null, "draw/arc center", "draw/arc", "draw/arc 3 point", "draw/pie",
            null, "draw/rectangle", "draw/rotated rectangle", "draw/circle", "draw/polygon"));
        MainMenu.Items.Add(MenuOf("_Tools", "tools/select", "tools/lasso", "tools/eraser", "tools/paint", "tools/tag", null, "tools/move", "tools/rotate",
            "tools/scale", "tools/flip", null, "tools/pushpull", "tools/followme", "tools/offset", null, "tools/tape", "tools/dimension",
            "tools/protractor", "tools/text", "tools/axes", "tools/3d text", "tools/section plane", null, "edit/reset axes"));
        MainMenu.Items.Add(MenuOf("_Window", "window/default tray"));
        MainMenu.Items.Add(MenuOf("_Help", "app/search", null, "help/shortcuts", null, "help/about"));
    }

    /// <summary>File &gt; Open Recent: the last files opened or saved; a file that has gone is dropped from the list.</summary>
    private void FillRecent(MenuItem recent)
    {
        recent.Items.Clear();
        foreach (var path in RecentFiles.Items)
        {
            var item = new MenuItem { Header = path.Replace("_", "__"), ToolTip = path };
            item.Click += (_, _) =>
            {
                // As File > Open: a tool half-way through an edit must not touch the next model.
                _tools.CancelActive();
                if (System.IO.File.Exists(path))
                {
                    if (_files.ConfirmDiscardChanges()) _files.Open(path);
                }
                else
                {
                    RecentFiles.Remove(path);
                    MessageBox.Show(this, "The file no longer exists: " + path, "Sketch", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            recent.Items.Add(item);
        }
        if (recent.Items.Count == 0) recent.Items.Add(new MenuItem { Header = "(no recent files)", IsEnabled = false });
    }

    private MenuItem MenuOf(string header, params string?[] keys)
    {
        var menu = new MenuItem { Header = header };
        foreach (var k in keys)
        {
            if (k is null) menu.Items.Add(new Separator());
            else menu.Items.Add(ItemFor(_byKey[k]));
        }
        if (menu.Items.Count == 0) menu.Items.Add(new MenuItem { Header = "(coming soon)", IsEnabled = false });
        return menu;
    }

    private MenuItem ItemFor(AppCommand command)
    {
        var item = new MenuItem
        {
            Header = command.Title,
            InputGestureText = command.ShortcutText,
            ToolTip = command.Description,
        };
        if (command.IconKey is not null && TryFindResource(command.IconKey) is ImageSource img)
            item.Icon = new Image { Source = img, Width = 16, Height = 16 };
        item.Click += (_, _) => Run(command);
        if (command.IsChecked is not null) _checkableItems.Add((item, command));
        return item;
    }

    private ToggleButton ButtonFor(AppCommand cmd)
    {
        var button = new ToggleButton
        {
            Style = (Style)FindResource("ToolButton"),
            ToolTip = cmd.ToolTipText,
            Content = new Image { Source = (ImageSource)FindResource(cmd.IconKey!) },
        };
        button.Click += (_, _) => Run(cmd);
        _toggleButtons.Add((button, cmd));
        return button;
    }

    private void RefreshToolState()
    {
        if (_materialsSection is not null) OpenPanelForTool();
        foreach (var (button, cmd) in _toggleButtons)
            button.IsChecked = cmd.IsChecked?.Invoke() ?? false;
        foreach (var (item, cmd) in _checkableItems)
            item.IsChecked = cmd.IsChecked?.Invoke() ?? false;
        RefreshSplitButtons();
        StatusText.Text = _tools.StatusText;
        MeasurementsLabel.Text = _tools.ActiveTool.MeasurementLabel;
        if (!MeasurementsActive) MeasurementsBox.Text = _tools.ActiveTool.MeasurementValue;
    }

    private void RefreshTitle()
    {
        var name = _files.CurrentPath is null ? "Untitled" : System.IO.Path.GetFileNameWithoutExtension(_files.CurrentPath);
        Title = $"{(Viewport.Model.IsModified ? "*" : string.Empty)}{name} - Sketch";
    }

    private void ShowCursorTip(string? text, double x, double y)
    {
        if (string.IsNullOrEmpty(text))
        {
            CursorTip.IsOpen = false;
            return;
        }
        var source = PresentationSource.FromVisual(this);
        var scale = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        CursorTipText.Text = text;
        CursorTip.HorizontalOffset = x * scale + 16;
        CursorTip.VerticalOffset = y * scale + 18;
        CursorTip.IsOpen = true;
    }

    private void ShowAbout() =>
        MessageBox.Show(this,
            "Sketch — a 3D modeling application for personal use.\n\n" + Viewport.GraphicsInfo,
            "About Sketch", MessageBoxButton.OK, MessageBoxImage.Information);

    private void ShowShortcuts()
    {
        var lines = _commands.Where(c => c.HasShortcut).OrderBy(c => c.Title).Select(c => $"{c.ShortcutText,-14}  {c.Title}");
        var extra = "\n\nMouse: middle button = Orbit, Shift + middle button = Pan, wheel = Zoom.\n" +
                    "While drawing: Shift = lock direction; ↑ blue, → red, ← green = lock to axis; Esc = cancel.\n" +
                    "Measurements box: while drawing, type a value and press Enter (e.g. 1200, 1.2m, 2000;1000, [x;y;z]).";
        MessageBox.Show(this, string.Join("\n", lines) + extra, "Keyboard Shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ---------------- Keyboard & Measurements box ----------------

    private void FocusViewport()
    {
        if (MeasurementsBox.IsKeyboardFocusWithin) MeasurementsBox.Text = string.Empty;
        FocusSinkFocus();
    }

    /// <summary>
    /// Gives keyboard focus to an invisible element. Focusing the window itself is not enough: WPF hands focus
    /// back to the window's last focused element (the Measurements box), which then swallowed Ctrl+Z, Ctrl+H, ...
    /// </summary>
    private void FocusSinkFocus()
    {
        FocusManager.SetFocusedElement(this, FocusSink);
        Keyboard.Focus(FocusSink);
    }

    private bool MeasurementsActive => MeasurementsBox.IsKeyboardFocusWithin;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (MeasurementsActive)
        {
            // Ctrl shortcuts act on the model even while a value is being typed (Ctrl+Z undoes the last
            // operation, not the text in the box).
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && key is not (Key.LeftCtrl or Key.RightCtrl))
            {
                MeasurementsBox.Text = string.Empty;
                FocusSinkFocus();
                if (ProcessKey(key, Keyboard.Modifiers)) e.Handled = true;
            }
            return;
        }
        if (e.OriginalSource is TextBox) return;
        if (e.IsRepeat && key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl)
        {
            e.Handled = true;
            return;
        }
        if (ProcessKey(key, Keyboard.Modifiers)) e.Handled = true;
    }

    /// <summary>Routes a key press: first to the active tool, then to command shortcuts.</summary>
    private bool ProcessKey(Key key, ModifierKeys modifiers)
    {
        if (_tools.KeyDown(ToViewKey(key), ToToolModifiers(modifiers)))
        {
            RefreshToolState();
            return true;
        }
        var command = _commands.FirstOrDefault(c => c.Matches(key, modifiers));
        if (command is null) return false;
        Run(command);
        return true;
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (MeasurementsActive) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_tools.KeyUp(ToViewKey(key), ToToolModifiers(Keyboard.Modifiers))) e.Handled = true;
    }

    /// <summary>Typing a number anywhere starts an entry in the Measurements box, as in SketchUp.</summary>
    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (MeasurementsActive || e.OriginalSource is TextBox || string.IsNullOrEmpty(e.Text)) return;
        var c = e.Text[0];
        if (char.IsDigit(c) || "-.,;[]<>*/'\"".Contains(c))
        {
            StartMeasurement(e.Text);
            e.Handled = true;
        }
    }

    private void StartMeasurement(string text)
    {
        MeasurementsBox.Text = text;
        MeasurementsBox.Focus();
        MeasurementsBox.CaretIndex = MeasurementsBox.Text.Length;
    }

    private void OnMeasurementsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var text = MeasurementsBox.Text;
            if (!string.IsNullOrWhiteSpace(text) && !_tools.UserText(text))
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }
            FocusSinkFocus();
            RefreshToolState();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Esc while typing abandons the entry and cancels the operation, as in SketchUp.
            MeasurementsBox.Text = string.Empty;
            FocusSinkFocus();
            _tools.KeyDown(ViewKey.Escape, KeyModifiers.None);
            RefreshToolState();
            e.Handled = true;
        }
    }

    private static ViewKey ToViewKey(Key key) => key switch
    {
        Key.Escape => ViewKey.Escape,
        Key.Enter => ViewKey.Enter,
        Key.LeftShift or Key.RightShift => ViewKey.Shift,
        Key.LeftCtrl or Key.RightCtrl => ViewKey.Ctrl,
        Key.LeftAlt or Key.RightAlt => ViewKey.Alt,
        Key.Up => ViewKey.Up,
        Key.Down => ViewKey.Down,
        Key.Left => ViewKey.Left,
        Key.Right => ViewKey.Right,
        _ => ViewKey.Other,
    };

    private static KeyModifiers ToToolModifiers(ModifierKeys m)
    {
        var r = KeyModifiers.None;
        if (m.HasFlag(ModifierKeys.Shift)) r |= KeyModifiers.Shift;
        if (m.HasFlag(ModifierKeys.Control)) r |= KeyModifiers.Ctrl;
        if (m.HasFlag(ModifierKeys.Alt)) r |= KeyModifiers.Alt;
        return r;
    }
}
