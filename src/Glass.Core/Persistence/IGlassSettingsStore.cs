using Glass.Core.Appearance;

namespace Glass.Core.Persistence;

public interface IGlassSettingsStore
{
    ValueTask<GlassSettings> LoadAsync(
        GlassSettings fallback,
        CancellationToken cancellationToken = default);

    ValueTask SaveAsync(
        GlassSettings settings,
        CancellationToken cancellationToken = default);
}
