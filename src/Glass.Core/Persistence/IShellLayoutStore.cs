using Glass.Core.Shell;

namespace Glass.Core.Persistence;

public interface IShellLayoutStore
{
    ValueTask<ShellLayout> LoadAsync(
        ShellLayout fallback,
        CancellationToken cancellationToken = default);

    ValueTask SaveAsync(
        ShellLayout layout,
        CancellationToken cancellationToken = default);
}
