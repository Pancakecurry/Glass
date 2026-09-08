using Glass.Core.Placement;

namespace Glass.Core.Shell;

public sealed record ShellLayout(IReadOnlyList<BarDefinition> Bars)
{
    public static ShellLayout CreateDefault(DisplayTarget target) =>
        new([BarDefinition.CreateDefault(target)]);

    public ShellLayout Normalize() =>
        new(Bars.Select(bar => bar.Normalize()).ToArray());
}
