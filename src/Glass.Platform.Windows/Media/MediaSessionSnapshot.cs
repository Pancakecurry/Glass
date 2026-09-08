namespace Glass.Platform.Windows.Media;

public sealed record MediaSessionSnapshot(
    bool IsAvailable,
    string SourceApplication,
    string Title,
    string Artist,
    string AlbumTitle,
    byte[]? Artwork,
    string PlaybackState,
    TimeSpan Position,
    TimeSpan Duration,
    bool CanPlay,
    bool CanPause,
    bool CanSkipPrevious,
    bool CanSkipNext,
    bool CanSeek,
    string Status)
{
    public static MediaSessionSnapshot NoSession { get; } =
        new(false, string.Empty, string.Empty, string.Empty, string.Empty, null, "None",
            TimeSpan.Zero, TimeSpan.Zero, false, false, false, false, false,
            "No active media session");

    public static MediaSessionSnapshot Unavailable(string message) =>
        new(false, string.Empty, string.Empty, string.Empty, string.Empty, null, "Unavailable",
            TimeSpan.Zero, TimeSpan.Zero, false, false, false, false, false, message);
}
