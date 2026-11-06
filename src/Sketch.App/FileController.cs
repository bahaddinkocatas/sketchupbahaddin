using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Sketch.Geometry;
using Sketch.IO;
using Sketch.Model;

namespace Sketch.App;

/// <summary>New / Open / Save / Save As, including the "save changes?" prompt.</summary>
public sealed class FileController
{
    private const string Filter = "Sketch model (*.sketch)|*.sketch|All files (*.*)|*.*";
    private const string OpenFilter = "Models (*.sketch;*.skp)|*.sketch;*.skp|Sketch model (*.sketch)|*.sketch|SketchUp model (*.skp)|*.skp|All files (*.*)|*.*";

    private readonly Window _owner;
    private readonly SketchModel _model;
    private readonly Camera _camera;
    private readonly Action _cancelTool;

    public FileController(Window owner, SketchModel model, Camera camera, Action cancelTool)
    {
        _owner = owner;
        _model = model;
        _camera = camera;
        _cancelTool = cancelTool;
    }

    public string? CurrentPath { get; private set; }

    /// <summary>Raised after a new or opened document replaced the model.</summary>
    public event EventHandler? FileChanged;

    /// <summary>Asks whether to save unsaved changes. Returns false when the user cancels.</summary>
    public bool ConfirmDiscardChanges()
    {
        if (!_model.IsModified) return true;
        var answer = MessageBox.Show(_owner, "Save changes to the model?", "Sketch",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer switch
        {
            MessageBoxResult.Yes => Save(),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    public void New()
    {
        _cancelTool();
        if (!ConfirmDiscardChanges()) return;
        ResetToTemplate(_model);
        _model.ResetHistory();
        CurrentPath = null;
        FileChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Empties the model and loads the thermal-modelling template (tags, materials, scenes).</summary>
    public static void ResetToTemplate(SketchModel model)
    {
        model.Entities.Clear();
        model.Tags.Clear();
        model.Materials.Clear();
        model.Scenes.Clear();
        model.ActiveTag = null;
        model.Axes = ModelAxes.World;
        model.Style = new RenderStyle();
        Sketch.Model.Templates.ThermalTemplate.Apply(model);
    }

    public void OpenWithDialog()
    {
        _cancelTool();
        if (!ConfirmDiscardChanges()) return;
        var dialog = new OpenFileDialog { Filter = OpenFilter, DefaultExt = SketchFile.Extension };
        if (dialog.ShowDialog(_owner) == true) Open(dialog.FileName);
    }

    public void Open(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".skp", StringComparison.OrdinalIgnoreCase))
        {
            OpenSkp(path);
            return;
        }
        try
        {
            var doc = SketchFile.Load(path);
            if (doc.Tags is null)
            {
                // Version-1 files have no tags/materials/scenes: start from the template.
                ResetToTemplate(_model);
                doc = doc with
                {
                    Tags = _model.Tags.Items.Select(t => new TagData(t.Name, t.Color.ToHex(), t.Visible)).ToList(),
                    Materials = _model.Materials.Items.Select(m => new MaterialData(m.Name, m.Color.ToHex(), m.Alpha)).ToList(),
                    Scenes = SketchDocument.From(_model, _camera).Scenes,
                };
            }
            doc.ApplyTo(_model, new Camera());
            _model.ResetHistory();
            CurrentPath = path;
            RecentFiles.Add(path);
            FileChanged?.Invoke(this, EventArgs.Empty);
            if (doc.Camera is { } cam) SketchFile.Apply(cam, _camera);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            MessageBox.Show(_owner, $"Could not open the file:\n{ex.Message}", "Sketch", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Opens a SketchUp file as a new model (thermal template + the file's tags, materials and geometry).</summary>
    private void OpenSkp(string path)
    {
        ResetToTemplate(_model);
        var templateArrows = NorthArrows().ToList();
        if (!ImportSkp(path, intoCurrent: false)) return;
        // The file's own north arrow replaces the default one (DEAP orientations follow the file's arrow).
        if (NorthArrows().Count() > templateArrows.Count) _model.Entities.Erase(templateArrows);
        _model.ResetHistory();
        // .skp cannot be written back: saving asks for a .sketch file name next to the original.
        CurrentPath = null;
        RecentFiles.Add(path);
        SuggestedName = Path.GetFileNameWithoutExtension(path);
        SuggestedFolder = Path.GetDirectoryName(path);
        FileChanged?.Invoke(this, EventArgs.Empty);
    }

    private IEnumerable<Entity> NorthArrows() =>
        _model.Entities.Groups.Where(g => System.Text.RegularExpressions.Regex.IsMatch(g.Name, @"north\s*arrow|northarrow",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase));

    public string? SuggestedName { get; private set; }
    public string? SuggestedFolder { get; private set; }

    /// <summary>File &gt; Import &gt; SketchUp model: adds a .skp file's content to the current model.</summary>
    public void ImportSkpWithDialog()
    {
        _cancelTool();
        var dialog = new OpenFileDialog { Filter = "SketchUp model (*.skp)|*.skp" };
        if (dialog.ShowDialog(_owner) != true) return;
        _model.StartOperation("Import");
        if (ImportSkp(dialog.FileName, intoCurrent: true)) _model.CommitOperation();
        else _model.AbortOperation();
        FileChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool ImportSkp(string path, bool intoCurrent)
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Sketch.IO.Skp.SkpImporter.Import(path, _model);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Any failure while reading (also an unusual or damaged file the reader does not expect) leaves the model
            // as it was, never half-imported: the caller undoes a started import.
            App.Log(ex);
            MessageBox.Show(_owner,
                $"Could not open the SketchUp file:\n{ex.Message}\n\nFiles saved by SketchUp 2021 or newer open without SketchUp installed.",
                "Sketch", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (!intoCurrent) ResetToTemplate(_model);
            return false;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    public bool Save() => CurrentPath is null ? SaveAs() : SaveTo(CurrentPath);

    public bool SaveAs()
    {
        _cancelTool();
        var dialog = new SaveFileDialog
        {
            Filter = Filter,
            DefaultExt = SketchFile.Extension,
            FileName = CurrentPath is null ? SuggestedName ?? "Untitled" : Path.GetFileName(CurrentPath),
            InitialDirectory = CurrentPath is null ? SuggestedFolder ?? string.Empty : Path.GetDirectoryName(CurrentPath) ?? string.Empty,
        };
        return dialog.ShowDialog(_owner) == true && SaveTo(dialog.FileName);
    }

    private bool SaveTo(string path)
    {
        try
        {
            _cancelTool();
            SketchFile.Save(path, SketchDocument.From(_model, _camera));
            CurrentPath = path;
            RecentFiles.Add(path);
            _model.MarkSaved();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(_owner, $"Could not save the file:\n{ex.Message}", "Sketch", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
}
