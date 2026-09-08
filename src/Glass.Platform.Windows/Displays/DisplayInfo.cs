using Microsoft.UI;
using Windows.Graphics;

namespace Glass.Platform.Windows.Displays;

public sealed record DisplayInfo(
    DisplayId Id,
    string PersistentId,
    string Name,
    bool IsPrimary,
    RectInt32 Bounds,
    RectInt32 WorkArea);
