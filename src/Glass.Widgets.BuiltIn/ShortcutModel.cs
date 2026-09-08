namespace Glass.Widgets.BuiltIn;

public enum ShortcutTargetKind { Application, File, Folder }

public sealed record ShortcutEntry(
    Guid ShortcutId,
    string DisplayName,
    ShortcutTargetKind Kind,
    string Target);

public sealed record ShortcutWidgetState(IReadOnlyList<ShortcutEntry> Entries)
{
    public ShortcutWidgetState Add(ShortcutEntry entry) =>
        new([.. Entries.Where(item => item.ShortcutId != entry.ShortcutId), entry]);

    public ShortcutWidgetState Remove(Guid shortcutId) =>
        new(Entries.Where(entry => entry.ShortcutId != shortcutId).ToArray());
}
