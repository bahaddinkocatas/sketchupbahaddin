using System.Windows.Input;

namespace Sketch.App.Commands;

/// <summary>
/// One entry of the command catalog: every menu item, toolbar button and shortcut is defined once here.
/// </summary>
public sealed class AppCommand
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public Key ShortcutKey { get; init; } = System.Windows.Input.Key.None;
    public ModifierKeys ShortcutModifiers { get; init; } = ModifierKeys.None;
    public string? IconKey { get; init; }
    public required Action Execute { get; init; }
    public Func<bool>? IsChecked { get; init; }

    public bool HasShortcut => ShortcutKey != System.Windows.Input.Key.None;

    public string ShortcutText
    {
        get
        {
            if (!HasShortcut) return string.Empty;
            var parts = new List<string>();
            if (ShortcutModifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (ShortcutModifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (ShortcutModifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            parts.Add(ShortcutKey switch
            {
                System.Windows.Input.Key.Space => "Space",
                System.Windows.Input.Key.Delete => "Delete",
                _ => ShortcutKey.ToString(),
            });
            return string.Join("+", parts);
        }
    }

    public string ToolTipText => HasShortcut ? $"{Title} ({ShortcutText})" : Title;

    public bool Matches(Key key, ModifierKeys modifiers) =>
        HasShortcut && key == ShortcutKey && modifiers == ShortcutModifiers;
}
