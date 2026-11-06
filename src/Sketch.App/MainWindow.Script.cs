using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Sketch.Tools;
using MouseButton = Sketch.Tools.MouseButton;

namespace Sketch.App;

/// <summary>
/// Test mode: <c>Sketch.App.exe --script file.txt</c> replays tool input inside the application (no real mouse or
/// keyboard is used, so other programs are never touched) and can save screenshots. Coordinates are view pixels.
/// <code>
/// key R | key ctrl+z | keydown shift | keyup shift
/// move x y | click x y [shift|ctrl] | down x y | up x y | dblclick x y
/// orbit x0 y0 x1 y1 | wheel x y delta | type 1200 | shot file.png | log text | wait ms | exit
/// </code>
/// </summary>
public partial class MainWindow
{
    private Queue<string>? _script;
    private string _scriptDir = string.Empty;
    private StreamWriter? _scriptLog;

    public void RunScript(string path)
    {
        _scriptDir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
        _script = new Queue<string>(File.ReadAllLines(path));
        _scriptLog = new StreamWriter(Path.Combine(_scriptDir, Path.GetFileNameWithoutExtension(path) + ".log")) { AutoFlush = true };
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(40) };
        var waitUntil = DateTime.MinValue;
        timer.Tick += (_, _) =>
        {
            if (DateTime.UtcNow < waitUntil) return;
            if (_script.Count == 0)
            {
                timer.Stop();
                return;
            }
            var line = _script.Dequeue().Trim();
            if (line.Length == 0 || line.StartsWith('#')) return;
            try
            {
                var wait = Step(line);
                if (wait > 0) waitUntil = DateTime.UtcNow.AddMilliseconds(wait);
            }
            catch (Exception ex)
            {
                _scriptLog.WriteLine($"HATA '{line}': {ex.Message}");
            }
        };
        timer.Start();
    }

    /// <summary>Runs everything queued on the UI thread (background refreshes, layout, rendering) before returning.</summary>
    private void PumpUi()
    {
        var frame = new DispatcherFrame();
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private int Step(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        double D(int i) => double.Parse(parts[i], CultureInfo.InvariantCulture);
        KeyModifiers Mods(int from)
        {
            var m = KeyModifiers.None;
            for (var i = from; i < parts.Length; i++)
            {
                if (parts[i] == "shift") m |= KeyModifiers.Shift;
                if (parts[i] == "ctrl") m |= KeyModifiers.Ctrl;
                if (parts[i] == "alt") m |= KeyModifiers.Alt;
            }
            return m;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "move":
                _tools.MouseMove(new ViewMouseEvent(D(1), D(2), MouseButton.None, Mods(3)));
                break;
            case "down":
                _tools.MouseDown(new ViewMouseEvent(D(1), D(2), MouseButton.Left, Mods(3)));
                break;
            case "up":
                _tools.MouseUp(new ViewMouseEvent(D(1), D(2), MouseButton.Left, Mods(3)));
                break;
            case "click":
                _tools.MouseMove(new ViewMouseEvent(D(1), D(2), MouseButton.None, Mods(3)));
                _tools.MouseDown(new ViewMouseEvent(D(1), D(2), MouseButton.Left, Mods(3)));
                _tools.MouseUp(new ViewMouseEvent(D(1), D(2), MouseButton.Left, Mods(3)));
                break;
            case "movew":
            case "clickw":
            {
                // movew / clickw x y z: the mouse goes to the screen position of a model point given in mm.
                Sketch.Tools.IView view = Viewport;
                var world = new Sketch.Geometry.Vector3d(D(1), D(2), D(3)) / 25.4;
                if (view.Camera.WorldToScreen(world, view.Width, view.Height) is not { } s) break;
                _tools.MouseMove(new ViewMouseEvent(s.X, s.Y, MouseButton.None, Mods(4)));
                if (parts[0] == "clickw")
                {
                    _tools.MouseDown(new ViewMouseEvent(s.X, s.Y, MouseButton.Left, Mods(4)));
                    _tools.MouseUp(new ViewMouseEvent(s.X, s.Y, MouseButton.Left, Mods(4)));
                }
                break;
            }
            case "dblclick":
                _tools.MouseDown(new ViewMouseEvent(D(1), D(2), MouseButton.Left, Mods(3)));
                _tools.MouseUp(new ViewMouseEvent(D(1), D(2), MouseButton.Left, Mods(3)));
                _tools.MouseDown(new ViewMouseEvent(D(1), D(2), MouseButton.Left, Mods(3)));
                _tools.DoubleClick(new ViewMouseEvent(D(1), D(2), MouseButton.Left, Mods(3)));
                _tools.MouseUp(new ViewMouseEvent(D(1), D(2), MouseButton.Left, Mods(3)));
                break;
            case "orbit":
            {
                _tools.MouseDown(new ViewMouseEvent(D(1), D(2), MouseButton.Middle, KeyModifiers.None));
                for (var i = 1; i <= 10; i++)
                    _tools.MouseMove(new ViewMouseEvent(D(1) + (D(3) - D(1)) * i / 10, D(2) + (D(4) - D(2)) * i / 10, MouseButton.None, KeyModifiers.None));
                _tools.MouseUp(new ViewMouseEvent(D(3), D(4), MouseButton.Middle, KeyModifiers.None));
                break;
            }
            case "wheel":
                _tools.MouseWheel(new ViewMouseEvent(D(1), D(2), MouseButton.None, KeyModifiers.None, (int)D(3)));
                break;
            case "key":
            {
                var spec = parts[1].ToLowerInvariant().Split('+');
                var modifiers = ModifierKeys.None;
                foreach (var s in spec[..^1])
                {
                    if (s == "ctrl") modifiers |= ModifierKeys.Control;
                    if (s == "shift") modifiers |= ModifierKeys.Shift;
                    if (s == "alt") modifiers |= ModifierKeys.Alt;
                }
                var name = spec[^1];
                var key = name.Length == 1 && char.IsLetter(name[0])
                    ? (Key)Enum.Parse(typeof(Key), name.ToUpperInvariant())
                    : (Key)Enum.Parse(typeof(Key), name, ignoreCase: true);
                ProcessKey(key, modifiers);
                break;
            }
            case "keydown":
                _tools.KeyDown(ParseViewKey(parts[1]), KeyModifiers.None);
                break;
            case "keyup":
                _tools.KeyUp(ParseViewKey(parts[1]), KeyModifiers.None);
                break;
            case "type":
            {
                var text = line[5..].Trim();
                var ok = _tools.UserText(text);
                _scriptLog?.WriteLine($"type '{text}' -> {(ok ? "kabul" : "RED")}");
                break;
            }
            case "shot":
                Viewport.SaveImage(Path.Combine(_scriptDir, parts[1]));
                break;
            case "log":
            {
                var ents = Viewport.Model.Entities;
                var active = Viewport.Model.ActiveEntities;
                _scriptLog?.WriteLine($"{line[4..].Trim()}: köşe={ents.Vertices.Count} kenar={ents.Edges.Count} yüz={ents.Faces.Count} grup={ents.Groups.Count} " +
                                      $"düzenleme-derinliği={Viewport.Model.EditPath.Count} geri-al={Viewport.Model.UndoName ?? "-"} işlem-açık={Viewport.Model.IsOperationActive} etkin-yüz={active.Faces.Count} tüm-yüz={ents.AllFaces().Count()} " +
                                      $"seçim={Viewport.Selection.Count} araç={_tools.ActiveTool.Id} ölçü='{_tools.ActiveTool.MeasurementValue}'");
                break;
            }
            case "tag":
            {
                // tag <name>: like choosing it in the toolbar tag selector.
                var name = line[4..].Trim();
                TagCombo.SelectedItem = ((IEnumerable<Panels.PanelHelpers.TagItem>)TagCombo.ItemsSource).FirstOrDefault(t => t.Name == name);
                break;
            }
            case "scene":
            {
                var name = line[6..].Trim();
                var scene = Viewport.Model.Scenes.Items.FirstOrDefault(s => s.Name == name);
                if (scene is not null) _scenes.Show(scene);
                break;
            }
            case "material":
            {
                var name = line[9..].Trim();
                _paintTool.Current = Viewport.Model.Materials.Find(name);
                _tools.Select(_paintTool);
                break;
            }
            case "shotui":
                SaveWindowImage(Path.Combine(_scriptDir, parts[1]));
                break;
            case "tool":
                // tool <command key>: runs any command, e.g. "tool tools/dimension".
                Run(_byKey[line[5..].Trim()]);
                break;
            case "import":
                ImportImage(line[7..].Trim());
                break;
            case "vcb":
            {
                // Types into the Measurements box like the keyboard would, then presses Enter through WPF routing.
                StartMeasurement(line[4..].Trim());
                var source = PresentationSource.FromVisual(MeasurementsBox);
                var args = new KeyEventArgs(Keyboard.PrimaryDevice, source!, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent };
                MeasurementsBox.RaiseEvent(args);
                break;
            }
            case "focus":
            {
                var focused = FocusManager.GetFocusedElement(this);
                _scriptLog?.WriteLine($"odak: {(focused is FrameworkElement fe ? fe.Name : focused?.GetType().Name ?? "yok")}");
                break;
            }
            case "open":
                _files.Open(line[5..].Trim());
                break;
            case "dump":
                foreach (var im in Viewport.Model.Entities.AllImages())
                    _scriptLog?.WriteLine($"  Görüntü {im.Name} köken={im.Origin} U={im.U} V={im.V} gizli={im.Hidden}");
                foreach (var f in Viewport.Model.Entities.Faces)
                {
                    var c = f.Vertices.Aggregate(Sketch.Geometry.Vector3d.Zero, (s, v) => s + v.Position) / f.Vertices.Count();
                    _scriptLog?.WriteLine($"  {f} n={f.Normal} merkez={c} delik={f.InnerLoops.Count}");
                }
                break;
            case "text3d":
            {
                // text3d <text>: like the 3D Text dialog with Arial, 300 mm letters, 50 mm deep.
                var loops = Panels.Text3DDialog.Outlines(line[7..].Trim(), "Arial", false, 300);
                _text3DTool.Prepare(loops, true, 50, line[7..].Trim());
                _tools.Select(_text3DTool);
                break;
            }
            case "pick":
            {
                // pick x y: what the Select tool would pick at a view point (for diagnosing selection).
                var q = new Sketch.Inference.InferenceQuery(Viewport.Camera, Viewport.PixelWidth, Viewport.PixelHeight, D(1), D(2));
                var ray = Viewport.Camera.RayThroughPixel(D(1), D(2), Viewport.PixelWidth, Viewport.PixelHeight);
                var picked = Viewport.Inference.PickEntity(q);
                var face = Viewport.Inference.FaceInference(ray);
                var image = Viewport.Inference.ImageHit(ray);
                var owner = picked is null ? null : Sketch.Model.Entities.AncestorIn(picked, Viewport.Model.ActiveEntities);
                _scriptLog?.WriteLine($"pick ({D(1)},{D(2)}): entity={picked?.GetType().Name ?? "yok"} grup={(owner as Sketch.Model.Group)?.Name ?? "-"} " +
                                      $"yüz={(face is null ? "yok" : face.Face?.ToString())} resim={(image is null ? "yok" : $"{image.Value.Image.Name} t={image.Value.T:0.##}")} " +
                                      $"kamera=persp:{Viewport.Camera.IsPerspective} göz={Viewport.Camera.Eye} yön={Viewport.Camera.Direction}");
                break;
            }
            case "sel":
                _scriptLog?.WriteLine($"  seçim: {string.Join(", ", Viewport.Selection.Items.GroupBy(e => e.GetType().Name).Select(g => $"{g.Key}={g.Count()}"))}");
                foreach (var f in Viewport.Selection.Items.OfType<Sketch.Model.Face>())
                    _scriptLog?.WriteLine($"    {f} n={f.Normal} merkez={f.Vertices.Aggregate(Sketch.Geometry.Vector3d.Zero, (acc, v) => acc + v.Position) / f.Vertices.Count()}");
                break;
            case "alltools":
            {
                // Smoke test: every tool gets a few clicks, a drag and Esc; exceptions are logged, not thrown.
                var points = new[] { (420.0, 450.0), (560.0, 470.0), (500.0, 380.0), (610.0, 420.0) };
                foreach (var cmd in _commands.Where(c => c.IsChecked is not null && (c.Key.StartsWith("tools/") || c.Key.StartsWith("draw/") || c.Key.StartsWith("camera/"))))
                {
                    if (cmd.Key == "tools/3d text") continue; // needs its dialog (covered by "text3d")
                    try
                    {
                        _tools.Select(_selectTool);
                        Viewport.Selection.Set(Viewport.Model.ActiveEntities.Faces.Take(1));
                        Run(cmd);
                        foreach (var (x, y) in points)
                        {
                            _tools.MouseMove(new ViewMouseEvent(x, y, MouseButton.None, KeyModifiers.None));
                            _tools.MouseDown(new ViewMouseEvent(x, y, MouseButton.Left, KeyModifiers.None));
                            _tools.MouseMove(new ViewMouseEvent(x + 15, y - 10, MouseButton.None, KeyModifiers.None));
                            _tools.MouseUp(new ViewMouseEvent(x + 15, y - 10, MouseButton.Left, KeyModifiers.None));
                        }
                        Viewport.SaveImage(Path.Combine(_scriptDir, "all-" + cmd.Key.Replace('/', '-').Replace(' ', '-') + ".png"));
                        ProcessKey(Key.Escape, ModifierKeys.None);
                        _scriptLog?.WriteLine($"OK {cmd.Key}: yüz={Viewport.Model.Entities.AllFaces().Count()} kenar={Viewport.Model.Entities.AllEdges().Count()} araç={_tools.ActiveTool.Id}");
                    }
                    catch (Exception ex)
                    {
                        _scriptLog?.WriteLine($"HATA {cmd.Key}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
                break;
            }
            case "pickcheck":
            {
                var (compared, bad) = Viewport.CheckPicking(parts.Length > 1 ? (int)D(1) : 12);
                _scriptLog?.WriteLine($"pickcheck: {compared} piksel, {bad.Count} uyumsuz");
                foreach (var m in bad.Take(25)) _scriptLog?.WriteLine("  " + m);
                break;
            }
            case "enter":
                // enter <name>: opens the top-level group with that name for editing; "enter" alone closes all groups.
                Viewport.Model.ExitAllGroups();
                if (line.Length > 5 && Viewport.Model.Entities.Groups.FirstOrDefault(g => g.Name == line[5..].Trim()) is { } entered)
                    Viewport.Model.EnterGroup(entered);
                break;
            case "dragbench":
            {
                // dragbench <tool> <group> [n]: inside the named group, presses the tool on its highest level face's
                // middle and drags n steps (3 px each) upwards, a frame per step; logs ms per step, then Esc.
                var hasSteps = parts.Length > 3 && int.TryParse(parts[^1], out _);
                var steps = hasSteps ? int.Parse(parts[^1]) : 40;
                var name = string.Join(' ', hasSteps ? parts[2..^1] : parts[2..]);
                // Group "-" = the loose geometry of the model root.
                Viewport.Model.ExitAllGroups();
                var container = Viewport.Model.Entities;
                if (name != "-")
                {
                    if (Viewport.Model.Entities.Groups.FirstOrDefault(g => g.Name == name) is not { } target) break;
                    Viewport.Model.EnterGroup(target);
                    container = target.Entities;
                }
                var top = container.Faces.Where(f => Math.Abs(f.Normal.Z) > 0.99).MaxBy(f => f.Vertices.Max(v => v.Position.Z) * 1e6 + Math.Abs(f.Area));
                if (top is null) break;
                var middle = top.Vertices.Aggregate(Sketch.Geometry.Vector3d.Zero, (s, v) => s + v.Position) / top.Vertices.Count();
                Sketch.Tools.IView view = Viewport;
                if (view.Camera.WorldToScreen(middle, view.Width, view.Height) is not { } at) break;
                Run(_byKey[parts[1]]);
                _tools.MouseMove(new ViewMouseEvent(at.X, at.Y, MouseButton.None, KeyModifiers.None));
                _tools.MouseDown(new ViewMouseEvent(at.X, at.Y, MouseButton.Left, KeyModifiers.None));
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var ui = TimeSpan.Zero;
                var tool = TimeSpan.Zero;
                for (var i = 1; i <= steps; i++)
                {
                    var t0 = sw.Elapsed;
                    _tools.MouseMove(new ViewMouseEvent(at.X, at.Y - 3 * i, MouseButton.Left, KeyModifiers.None));
                    tool += sw.Elapsed - t0;
                    // Then everything the change queued, as between real mouse moves: panel refreshes and the frame
                    // itself (the view repaints on WM_PAINT).
                    var before = sw.Elapsed;
                    PumpUi();
                    ui += sw.Elapsed - before;
                }
                _scriptLog?.WriteLine($"dragbench {parts[1]} in '{name}': {sw.Elapsed.TotalMilliseconds / steps:0.0} ms/adım (araç {tool.TotalMilliseconds / steps:0.0} ms, ekran ve paneller {ui.TotalMilliseconds / steps:0.0} ms)");
                ProcessKey(Key.Escape, ModifierKeys.None);
                Viewport.Model.ExitAllGroups();
                break;
            }
            case "splash":
            {
                // splash file.png: draws the splash screen (loading line half full) into an image.
                var splash = new SplashWindow(TimeSpan.FromSeconds(3));
                var card = (FrameworkElement)splash.Content;
                card.Measure(new Size(splash.Width, splash.Height));
                card.Arrange(new Rect(0, 0, splash.Width, splash.Height));
                splash.Fill.Width = splash.Track.ActualWidth / 2;
                card.UpdateLayout();
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)splash.Width, (int)splash.Height, 96, 96,
                    System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render(card);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (var stream = File.Create(Path.Combine(_scriptDir, parts[1]))) encoder.Save(stream);
                splash.Close();
                break;
            }
            case "select":
                // select <name>: selects the top-level group with that name.
                Viewport.Selection.Set(Viewport.Model.Entities.Groups.Where(g => g.Name == line[6..].Trim()));
                break;
            case "materials":
                _scriptLog?.WriteLine($"  malzemeler: {string.Join(", ", Viewport.Model.Materials.Items.Select(m => m.Name))}");
                _scriptLog?.WriteLine($"  görüntüler: {Viewport.Model.Entities.AllImages().Count()}, gruplar: {Viewport.Model.Entities.Groups.Count}");
                break;
            case "deap":
                // deap <folder>: runs the DEAP design-stage tool without the folder dialog.
                RunDeap(line[5..].Trim());
                break;
            case "bench":
            {
                // bench: times one frame, a frame after an edit, bounds, inference and picking.
                var sw = System.Diagnostics.Stopwatch.StartNew();
                double Time(Action a, int n) { sw.Restart(); for (var i = 0; i < n; i++) a(); return sw.Elapsed.TotalMilliseconds / n; }
                var frame = Viewport.BenchRender(10);
                var rebuild = Time(() => { Viewport.BumpSceneVersion(); Viewport.BenchRender(1); }, 3);
                var bounds = Time(() => _ = Viewport.ModelBounds, 10);
                var w = Viewport.PixelWidth; var h = Viewport.PixelHeight;
                var pts = Enumerable.Range(0, 20).Select(i => (X: w * (0.2 + 0.03 * i), Y: h * (0.3 + 0.02 * i))).ToList();
                var cam = Viewport.Camera;
                var firstInfer = Time(() => Viewport.Inference.Infer(new Sketch.Inference.InferenceQuery(cam, w, h, pts[0].X, pts[0].Y)), 1);
                var modelAndScreen = Time(() => { Viewport.Inference.Invalidate(); Viewport.Inference.Infer(new Sketch.Inference.InferenceQuery(cam, w, h, pts[0].X, pts[0].Y)); }, 3);
                var screenOnly = Time(() => { cam.Translate(new Sketch.Geometry.Vector3d(0.001, 0, 0)); Viewport.Inference.Infer(new Sketch.Inference.InferenceQuery(cam, w, h, pts[0].X, pts[0].Y)); }, 3);
                _scriptLog?.WriteLine($"  model önbelleği+ekran dizini={modelAndScreen:0.0}ms, yalnız ekran dizini (kamera değişince)={screenOnly:0.0}ms");
                var infer = Time(() => { foreach (var p in pts) Viewport.Inference.Infer(new Sketch.Inference.InferenceQuery(cam, w, h, p.X, p.Y)); }, 1) / pts.Count;
                var faceHit = Time(() => { foreach (var p in pts) Viewport.Inference.FaceInference(cam.RayThroughPixel(p.X, p.Y, w, h)); }, 1) / pts.Count;
                _scriptLog?.WriteLine($"  ilk çıkarım (dizin kurulumu dahil)={firstInfer:0.0}ms, yüz ışını={faceHit:0.00}ms");
                var inferFrom = Time(() => { foreach (var p in pts) Viewport.Inference.Infer(new Sketch.Inference.InferenceQuery(cam, w, h, p.X, p.Y) { From = Viewport.ModelBounds.Center }); }, 1) / pts.Count;
                var pick = Time(() => { foreach (var p in pts) Viewport.Inference.PickEntity(new Sketch.Inference.InferenceQuery(cam, w, h, p.X, p.Y)); }, 1) / pts.Count;
                var ents = Viewport.Model.Entities;
                var oneGroup = ents.Groups.FirstOrDefault(g => g.Entities.Faces.Count > 0);
                var edit = oneGroup is null ? -1 : Time(() =>
                {
                    var f = oneGroup.Entities.Faces[0];
                    f.Hidden = f.Hidden; // a property edit inside one dwelling
                    ents.Touch();
                    Viewport.BenchRender(1);
                    Viewport.Inference.Infer(new Sketch.Inference.InferenceQuery(cam, w, h, pts[0].X, pts[0].Y));
                }, 3);
                _scriptLog?.WriteLine($"  tek dairede düzenleme sonrası kare + ilk çıkarım={edit:0.0}ms");
                _scriptLog?.WriteLine($"bench: yüz={ents.AllFaces().Count()} kenar={ents.AllEdges().Count()} | kare={frame:0.0}ms, düzenleme sonrası kare={rebuild:0.0}ms, sınırlar={bounds:0.0}ms, çıkarım={infer:0.0}ms, çıkarım(From)={inferFrom:0.0}ms, seçim={pick:0.0}ms");
                break;
            }
            case "movebench":
            {
                // movebench x0 y0 x1 y1 n: n mouse moves from (x0,y0) to (x1,y1), each followed by a frame; logs ms per move.
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var n = (int)D(5);
                for (var i = 1; i <= n; i++)
                {
                    _tools.MouseMove(new ViewMouseEvent(D(1) + (D(3) - D(1)) * i / n, D(2) + (D(4) - D(2)) * i / n, MouseButton.None, KeyModifiers.None));
                    Viewport.BenchRender(1);
                }
                _scriptLog?.WriteLine($"movebench: {sw.Elapsed.TotalMilliseconds / n:0.0} ms/hareket ({_tools.ActiveTool.Id})");
                break;
            }
            case "wait":
                return (int)D(1);
            case "exit":
                Viewport.Model.MarkSaved();
                _scriptLog?.Dispose();
                Application.Current.Shutdown();
                break;
            default:
                _scriptLog?.WriteLine($"Bilinmeyen komut: {line}");
                break;
        }
        RefreshToolState();
        return 0;
    }

    /// <summary>Saves the whole window (panels included); the 3D view is rendered off-screen and composited in.</summary>
    private void SaveWindowImage(string path)
    {
        UpdateLayout();
        var source = PresentationSource.FromVisual(this);
        var dpi = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var width = (int)Math.Round(ActualWidth * dpi);
        var height = (int)Math.Round(ActualHeight * dpi);
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96 * dpi, 96 * dpi, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(this);

        var temp = Path.Combine(Path.GetTempPath(), $"sketch-view-{Guid.NewGuid():N}.png");
        Viewport.SaveImage(temp);
        var viewImage = new System.Windows.Media.Imaging.PngBitmapDecoder(new Uri(temp), System.Windows.Media.Imaging.BitmapCreateOptions.None,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames[0];
        var origin = Viewport.TranslatePoint(new Point(0, 0), this);

        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(rtb, new Rect(0, 0, ActualWidth, ActualHeight));
            dc.DrawImage(viewImage, new Rect(origin.X, origin.Y, Viewport.ActualWidth, Viewport.ActualHeight));
        }
        var final = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96 * dpi, 96 * dpi, System.Windows.Media.PixelFormats.Pbgra32);
        final.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(final));
        using (var stream = File.Create(path)) encoder.Save(stream);
        File.Delete(temp);
    }

    private static ViewKey ParseViewKey(string s) => s.ToLowerInvariant() switch
    {
        "shift" => ViewKey.Shift,
        "ctrl" => ViewKey.Ctrl,
        "alt" => ViewKey.Alt,
        "up" => ViewKey.Up,
        "down" => ViewKey.Down,
        "left" => ViewKey.Left,
        "right" => ViewKey.Right,
        "escape" or "esc" => ViewKey.Escape,
        _ => ViewKey.Other,
    };
}
