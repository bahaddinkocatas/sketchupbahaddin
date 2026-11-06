using System.IO;

namespace Sketch.App;

/// <summary>The last files opened or saved (File &gt; Open Recent), kept in the user's local app data folder.</summary>
internal static class RecentFiles
{
    private const int Max = 10;
    private static List<string>? _items;

    private static string StorePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sketch", "recent.txt");

    public static IReadOnlyList<string> Items => _items ??= Load();

    private static List<string> Load()
    {
        try
        {
            return File.Exists(StorePath)
                ? File.ReadAllLines(StorePath).Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(Max).ToList()
                : new List<string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new List<string>();
        }
    }

    /// <summary>Puts a file at the top of the list.</summary>
    public static void Add(string path)
    {
        var list = Items.ToList();
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        Save(list.Take(Max).ToList());
    }

    /// <summary>Drops a file that no longer exists.</summary>
    public static void Remove(string path) =>
        Save(Items.Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase)).ToList());

    private static void Save(List<string> list)
    {
        _items = list;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            File.WriteAllLines(StorePath, list);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // the list is a convenience; failing to store it is not an error
        }
    }
}
