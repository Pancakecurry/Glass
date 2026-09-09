using System.Globalization;
using Glass.Widgets.Abstractions;
using Glass.Widgets.Runtime;

namespace Glass.Widgets.BuiltIn;

public sealed class CalculatorWidgetInstance(WidgetInstanceConfiguration configuration)
    : BuiltInWidgetInstanceBase(configuration)
{
    public string Expression { get; private set; } = string.Empty;
    public string Result { get; private set; } = "0";

    public void Append(string value) { Expression += value; Publish(); }
    public void Clear() { Expression = string.Empty; Result = "0"; Publish(); }
    public void Evaluate()
    {
        try
        {
            Result = CalculatorExpression.Evaluate(Expression)
                .ToString("G12", CultureInfo.CurrentCulture);
        }
        catch (FormatException) { Result = "Check expression"; }
        catch (OverflowException) { Result = "Value too large"; }
        catch (DivideByZeroException) { Result = "Cannot divide by zero"; }
        Publish();
    }
}

public sealed class NotesWidgetInstance : BuiltInWidgetInstanceBase
{
    public NotesWidgetInstance(WidgetInstanceConfiguration configuration, WidgetStateStore store)
        : base(configuration) => Model = new QuickNotesModel(configuration.InstanceId, store);

    public QuickNotesModel Model { get; }

    protected override async ValueTask OnMountedAsync(CancellationToken token)
    {
        await Model.LoadAsync(token).ConfigureAwait(false);
        Publish();
    }

    public void Update(string text) { Model.Update(text); Publish(); }

    protected override async ValueTask OnDisposedAsync()
    {
        await Model.DisposeAsync().ConfigureAwait(false);
        await base.OnDisposedAsync().ConfigureAwait(false);
    }
}

public sealed class ShortcutsWidgetInstance : BuiltInWidgetInstanceBase
{
    private readonly WidgetStateStore _store;

    public ShortcutsWidgetInstance(WidgetInstanceConfiguration configuration, WidgetStateStore store)
        : base(configuration) => _store = store;

    public ShortcutWidgetState Shortcuts { get; private set; } = new([]);

    protected override async ValueTask OnMountedAsync(CancellationToken token)
    {
        Shortcuts = await _store.LoadAsync<ShortcutWidgetState>(Configuration.InstanceId, token)
            .ConfigureAwait(false) ?? new ShortcutWidgetState([]);
        Publish();
    }

    public async ValueTask AddAsync(ShortcutEntry entry, CancellationToken token = default)
    {
        Shortcuts = Shortcuts.Add(entry);
        await _store.SaveAsync(Configuration.InstanceId, Shortcuts, token).ConfigureAwait(false);
        Publish();
    }

    public async ValueTask RemoveAsync(Guid id, CancellationToken token = default)
    {
        Shortcuts = Shortcuts.Remove(id);
        await _store.SaveAsync(Configuration.InstanceId, Shortcuts, token).ConfigureAwait(false);
        Publish();
    }
}
