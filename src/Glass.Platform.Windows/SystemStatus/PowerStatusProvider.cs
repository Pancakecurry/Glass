using Glass.Widgets.Abstractions;
using Windows.System.Power;

namespace Glass.Platform.Windows.SystemStatus;

public sealed record PowerSnapshot(int? RemainingChargePercent, BatteryStatus Status,
    EnergySaverStatus EnergySaverStatus, PowerSupplyStatus PowerSupplyStatus);

public sealed class PowerStatusProvider : IWidgetProvider, IDisposable
{
    private bool _started;
    public string ProviderId => "power";
    public PowerSnapshot Current { get; private set; } = Read();
    public event Action<PowerSnapshot>? Changed;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_started) return ValueTask.CompletedTask;
        PowerManager.RemainingChargePercentChanged += OnChanged;
        PowerManager.BatteryStatusChanged += OnChanged;
        PowerManager.EnergySaverStatusChanged += OnChanged;
        PowerManager.PowerSupplyStatusChanged += OnChanged;
        _started = true;
        OnChanged(null, null);
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_started) return ValueTask.CompletedTask;
        PowerManager.RemainingChargePercentChanged -= OnChanged;
        PowerManager.BatteryStatusChanged -= OnChanged;
        PowerManager.EnergySaverStatusChanged -= OnChanged;
        PowerManager.PowerSupplyStatusChanged -= OnChanged;
        _started = false;
        return ValueTask.CompletedTask;
    }

    public void Dispose() { _ = StopAsync(); Changed = null; }
    private void OnChanged(object? sender, object? args)
    {
        Current = Read();
        Changed?.Invoke(Current);
    }
    private static PowerSnapshot Read() => new(PowerManager.RemainingChargePercent,
        PowerManager.BatteryStatus, PowerManager.EnergySaverStatus, PowerManager.PowerSupplyStatus);
}
