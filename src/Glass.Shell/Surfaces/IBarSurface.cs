using Glass.Core.Shell;

namespace Glass.Shell.Surfaces;

public sealed class BarDefinitionChangedEventArgs(BarDefinition definition) : EventArgs
{
    public BarDefinition Definition { get; } = definition;
}

public interface IBarSurface : IDisposable
{
    BarId Id { get; }

    BarDefinition Definition { get; }

    bool IsVisible { get; }

    Exception? LastFailure { get; }

    event EventHandler<BarDefinitionChangedEventArgs>? DefinitionSettled;

    void Apply(BarDefinition definition);

    BarDefinition ReconcileDisplay();

    void SetVisible(bool visible);

    void Close();
}

public interface IBarSurfaceFactory
{
    IBarSurface Create(BarDefinition definition);
}
