using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Sketch.Model;

namespace Sketch.App.Panels;

/// <summary>Scene tabs above the view: click to show a scene; right-click to update, rename, delete or add.</summary>
public sealed class ScenesBar : WrapPanel
{
    private readonly SketchModel _model;
    private readonly Func<Camera> _camera;
    private readonly Action _invalidate;
    private Scene? _current;

    public ScenesBar(SketchModel model, Func<Camera> camera, Action invalidate)
    {
        _model = model;
        _camera = camera;
        _invalidate = invalidate;
        Margin = new Thickness(4, 2, 4, 2);
        model.Scenes.Changed += (_, _) => Refresh();
        Refresh();
    }

    public void Refresh()
    {
        Children.Clear();
        foreach (var scene in _model.Scenes.Items)
        {
            var tab = new ToggleButton
            {
                Content = scene.Name,
                IsChecked = scene == _current,
                Style = (Style)FindResource("SceneTab"),
            };
            tab.Click += (_, _) => Show(scene);
            tab.ContextMenu = MenuFor(scene);
            Children.Add(tab);
        }
        var add = new Button { Content = "+", ToolTip = "Add Scene", Padding = new Thickness(8, 1, 8, 1), Focusable = false, Margin = new Thickness(2, 0, 0, 2) };
        add.Click += (_, _) => AddScene();
        Children.Add(add);
    }

    public void Show(Scene scene)
    {
        _current = scene;
        _model.ApplyScene(scene);
        if (scene.SavesCamera && scene.Camera is { } cam) _camera().CopyFrom(cam);
        _invalidate();
        Refresh();
    }

    private void AddScene()
    {
        var name = Prompt.Show(Window.GetWindow(this), "Scene name:", "Add Scene", _model.Scenes.NextName());
        if (string.IsNullOrWhiteSpace(name)) return;
        var scene = new Scene { Name = name };
        _model.Run("Add Scene", () =>
        {
            _model.UpdateScene(scene, _camera());
            _model.Scenes.Add(scene);
        });
        _current = _model.Scenes.Items.FirstOrDefault(s => s.Name == name);
        Refresh();
    }

    private ContextMenu MenuFor(Scene scene)
    {
        var menu = new ContextMenu();
        MenuItem Item(string header, Action action)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += (_, _) => action();
            menu.Items.Add(mi);
            return mi;
        }

        Item("Update Scene", () => _model.Run("Update Scene", () => _model.UpdateScene(scene, _camera())));
        var saveCam = Item("Save Camera Location", () =>
        {
            _model.Run("Scene Properties", () =>
            {
                scene.SavesCamera = !scene.SavesCamera;
                if (scene.SavesCamera && scene.Camera is null) scene.Camera = _camera().Clone();
            });
        });
        saveCam.IsCheckable = true;
        saveCam.IsChecked = scene.SavesCamera;
        Item("Rename Scene...", () =>
        {
            var name = Prompt.Show(Window.GetWindow(this), "New scene name:", "Rename Scene", scene.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            _model.Run("Rename Scene", () =>
            {
                scene.Name = name;
                _model.Scenes.RaiseChanged();
            });
        });
        Item("Move Left", () => _model.Run("Move Scene", () => _model.Scenes.Move(scene, _model.Scenes.Items.ToList().IndexOf(scene) - 1)));
        Item("Move Right", () => _model.Run("Move Scene", () => _model.Scenes.Move(scene, _model.Scenes.Items.ToList().IndexOf(scene) + 1)));
        menu.Items.Add(new Separator());
        Item("Delete Scene", () =>
        {
            _model.Run("Delete Scene", () => _model.Scenes.Remove(scene));
            if (_current == scene) _current = null;
        });
        Item("Add Scene...", AddScene);
        return menu;
    }
}
