using System.Globalization;
using Glass.Widgets.Abstractions;
using Glass.Widgets.Runtime;

namespace Glass.Widgets.BuiltIn;

public interface IBuiltInWidgetInstance : IWidgetInstance
{
    event EventHandler? Updated;
}

public sealed class BuiltInWidgetFactory(
    WidgetMetadata metadata,
    ProviderCoordinator? providers = null,
    WidgetStateStore? stateStore = null) : IWidgetFactory
{
    public WidgetMetadata Metadata { get; } = metadata;

    public IWidgetInstance Create(WidgetInstanceConfiguration configuration) =>
        configuration.TypeId.Value switch
        {
            "media" => new MediaWidgetInstance(configuration, providers),
            "clock" => new ClockWidgetInstance(configuration),
            "date" => new DateWidgetInstance(configuration),
            "cpu" => new CpuWidgetInstance(configuration, providers),
            "ram" => new RamWidgetInstance(configuration, providers),
            "network" => new NetworkWidgetInstance(configuration, providers),
            "storage" => new StorageWidgetInstance(configuration, providers),
            "battery" => new BatteryWidgetInstance(configuration, providers),
            "audio" => new AudioWidgetInstance(configuration, providers),
            "calendar" => new CalendarWidgetInstance(configuration),
            "timer" => new TimerWidgetInstance(configuration),
            "stopwatch" => new StopwatchWidgetInstance(configuration),
            "calculator" => new CalculatorWidgetInstance(configuration),
            "notes" => new NotesWidgetInstance(configuration, RequireStateStore(stateStore)),
            "clipboard" => new ClipboardWidgetInstance(configuration, providers),
            "storageUtility" => new StorageUtilityWidgetInstance(configuration, providers),
            "shortcuts" => new ShortcutsWidgetInstance(configuration, RequireStateStore(stateStore)),
            "weather" => new WeatherWidgetInstance(configuration, providers),
            "pomodoro" => new PomodoroWidgetInstance(configuration),
            _ => throw new ArgumentOutOfRangeException(
                nameof(configuration), configuration.TypeId, "Unknown built-in widget type."),
        };

    private static WidgetStateStore RequireStateStore(WidgetStateStore? store) =>
        store ?? throw new InvalidOperationException("This widget requires a local state store.");
}

public static class BuiltInWidgetRegistration
{
    public static void RegisterAll(
        WidgetRegistry registry,
        ProviderCoordinator? providers = null,
        WidgetStateStore? stateStore = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        foreach (var metadata in BuiltInWidgetCatalog.All)
            registry.Register(new BuiltInWidgetFactory(metadata, providers, stateStore));
    }
}

public abstract class BuiltInWidgetInstanceBase(WidgetInstanceConfiguration configuration)
    : WidgetInstanceBase(configuration), IBuiltInWidgetInstance
{
    public event EventHandler? Updated;
    protected void Publish() => Updated?.Invoke(this, EventArgs.Empty);
    protected override ValueTask OnDisposedAsync()
    {
        Updated = null;
        return ValueTask.CompletedTask;
    }
}

public abstract class ProviderWidgetInstance(
    WidgetInstanceConfiguration configuration,
    ProviderCoordinator? providers,
    params string[] providerIds) : BuiltInWidgetInstanceBase(configuration)
{
    private bool _visible;

    protected override async ValueTask OnVisibilityChangedAsync(
        bool visible,
        CancellationToken token)
    {
        if (_visible == visible) return;
        _visible = visible;
        if (providers is not null)
        {
            foreach (var providerId in providerIds)
                await providers.SetVisibleAsync(
                    providerId, Configuration.InstanceId, visible, token).ConfigureAwait(false);
        }
        Publish();
    }

    protected override async ValueTask OnDisposedAsync()
    {
        if (_visible && providers is not null)
        {
            foreach (var providerId in providerIds)
                await providers.SetVisibleAsync(
                    providerId, Configuration.InstanceId, false).ConfigureAwait(false);
        }
        await base.OnDisposedAsync().ConfigureAwait(false);
    }
}

public sealed class MediaWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "media");
public sealed class CpuWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "systemMetrics");
public sealed class RamWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "systemMetrics");
public sealed class NetworkWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "systemMetrics");
public sealed class StorageWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "systemMetrics");
public sealed class StorageUtilityWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "systemMetrics");
public sealed class BatteryWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "power");
public sealed class AudioWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "audio");
public sealed class ClipboardWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "clipboard");
public sealed class WeatherWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "weather");

public class ClockWidgetInstance : BuiltInWidgetInstanceBase
{
    private CancellationTokenSource? _ticker;
    public ClockWidgetInstance(WidgetInstanceConfiguration configuration) : base(configuration)
    {
        var settings = configuration.Settings;
        Model = new ClockModel(TimeProvider.System, new ClockOptions(
            settings.GetValueOrDefault("timeZoneId"),
            !settings.TryGetValue("use24Hour", out var value) || value != "false",
            settings.GetValueOrDefault("showSeconds") == "true"));
    }
    public ClockModel Model { get; }
    protected override ValueTask OnVisibilityChangedAsync(bool visible, CancellationToken token)
    {
        _ticker?.Cancel();
        _ticker?.Dispose();
        _ticker = visible ? CancellationTokenSource.CreateLinkedTokenSource(token) : null;
        if (_ticker is not null) _ = TickAsync(_ticker.Token);
        return ValueTask.CompletedTask;
    }
    protected override ValueTask OnDisposedAsync()
    {
        _ticker?.Cancel();
        _ticker?.Dispose();
        return base.OnDisposedAsync();
    }
    private async Task TickAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                Publish();
                await Task.Delay(Model.UntilNextBoundary(), TimeProvider.System, token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
}

public sealed class DateWidgetInstance(WidgetInstanceConfiguration configuration)
    : ClockWidgetInstance(configuration);

public sealed class CalendarWidgetInstance : BuiltInWidgetInstanceBase
{
    public CalendarWidgetInstance(WidgetInstanceConfiguration configuration) : base(configuration) =>
        Model = new CalendarModel(CultureInfo.CurrentCulture,
            DateOnly.FromDateTime(DateTime.Today));
    public CalendarModel Model { get; }
    public void Previous() { Model.PreviousMonth(); Publish(); }
    public void Next() { Model.NextMonth(); Publish(); }
    public void Today() { Model.GoToToday(TimeProvider.System); Publish(); }
    public void Select(DateOnly date) { Model.Select(date); Publish(); }
}

public sealed class TimerWidgetInstance : BuiltInWidgetInstanceBase
{
    public TimerWidgetInstance(WidgetInstanceConfiguration configuration) : base(configuration) { }
    public CountdownTimer Timer { get; } = new(TimeProvider.System);
    public void Start(TimeSpan duration) { Timer.Start(duration); Publish(); }
    public void Pause() { Timer.Pause(); Publish(); }
    public void Resume() { Timer.Resume(); Publish(); }
    public void Reset() { Timer.Reset(); Publish(); }
}

public sealed class StopwatchWidgetInstance : BuiltInWidgetInstanceBase
{
    public StopwatchWidgetInstance(WidgetInstanceConfiguration configuration) : base(configuration) { }
    public SemanticStopwatch Stopwatch { get; } = new(TimeProvider.System);
    public void Start() { Stopwatch.Start(); Publish(); }
    public void Pause() { Stopwatch.Pause(); Publish(); }
    public void Lap() { Stopwatch.Lap(); Publish(); }
    public void Reset() { Stopwatch.Reset(); Publish(); }
}

public sealed class PomodoroWidgetInstance : BuiltInWidgetInstanceBase
{
    public PomodoroWidgetInstance(WidgetInstanceConfiguration configuration) : base(configuration) =>
        Pomodoro = new PomodoroTimer(TimeProvider.System, new PomodoroOptions(
            TimeSpan.FromMinutes(ReadMinutes(configuration.Settings, "focusMinutes", 25)),
            TimeSpan.FromMinutes(ReadMinutes(configuration.Settings, "shortBreakMinutes", 5)),
            TimeSpan.FromMinutes(ReadMinutes(configuration.Settings, "longBreakMinutes", 15))));
    public PomodoroTimer Pomodoro { get; }
    public void Start() { Pomodoro.Start(); Publish(); }
    public void Pause() { Pomodoro.Timer.Pause(); Publish(); }
    public void Reset() { Pomodoro.Timer.Reset(); Publish(); }
    public void Advance() { Pomodoro.Advance(); Publish(); }
    private static double ReadMinutes(
        IReadOnlyDictionary<string, string> settings, string key, double fallback) =>
        settings.TryGetValue(key, out var value) &&
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 1, 180) : fallback;
}

public sealed class CalculatorWidgetInstance(WidgetInstanceConfiguration configuration)
    : BuiltInWidgetInstanceBase(configuration)
{
    public string Expression { get; private set; } = string.Empty;
    public string Result { get; private set; } = "0";
    public void Append(string value) { Expression += value; Publish(); }
    public void Clear() { Expression = string.Empty; Result = "0"; Publish(); }
    public void Evaluate()
    {
        try { Result = CalculatorExpression.Evaluate(Expression).ToString("G12", CultureInfo.CurrentCulture); }
        catch { Result = "Check expression"; }
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
