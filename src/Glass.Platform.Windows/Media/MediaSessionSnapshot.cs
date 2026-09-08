namespace Glass.Platform.Windows.Media;

public sealed record MediaSessionSnapshot(
    bool IsAvailable,
    string SourceApplication,
    string Title,
    string Artist,
    string PlaybackState,
    string Status)
{
    public static MediaSessionSnapshot NoSession { get; } =
        new(false, string.Empty, string.Empty, string.Empty, "None", "No active media session");

    public static MediaSessionSnapshot Unavailable(string message) =>
        new(false, string.Empty, string.Empty, string.Empty, "Unavailable", message);
}
