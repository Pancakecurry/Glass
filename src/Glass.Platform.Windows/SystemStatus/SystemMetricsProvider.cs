using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Glass.Widgets.Abstractions;

namespace Glass.Platform.Windows.SystemStatus;

public sealed record SystemMetricsSnapshot(
    double CpuPercent,
    ulong MemoryUsedBytes,
    ulong MemoryTotalBytes,
    long NetworkReceivedBytesPerSecond,
    long NetworkSentBytesPerSecond,
    IReadOnlyList<VolumeSnapshot> Volumes);

public sealed record VolumeSnapshot(string Name, string RootPath, long TotalBytes, long FreeBytes);

public sealed partial class SystemMetricsProvider : IWidgetProvider, IAsyncDisposable
{
    private readonly TimeProvider _timeProvider;
    private CancellationTokenSource? _sampling;
    private Task? _samplingTask;
    private CpuTimes? _previousCpu;
    private NetworkTotals? _previousNetwork;
    private DateTimeOffset _previousNetworkAt;

    public SystemMetricsProvider(TimeProvider? timeProvider = null) =>
        _timeProvider = timeProvider ?? TimeProvider.System;

    public string ProviderId => "systemMetrics";
    public SystemMetricsSnapshot? Current { get; private set; }
    public event Action<SystemMetricsSnapshot>? Changed;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (_sampling is not null) return ValueTask.CompletedTask;
        _sampling = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _samplingTask = SampleLoopAsync(_sampling.Token);
        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_sampling is null) return;
        _sampling.Cancel();
        try { if (_samplingTask is not null) await _samplingTask.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        _sampling.Dispose();
        _sampling = null;
        _samplingTask = null;
        _previousCpu = null;
        _previousNetwork = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        Changed = null;
    }

    private async Task SampleLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), _timeProvider);
        Sample();
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false)) Sample();
    }

    private void Sample()
    {
        var cpu = ReadCpu();
        var memory = ReadMemory();
        var network = ReadNetwork();
        var now = _timeProvider.GetUtcNow();
        var seconds = Math.Max(0.001, (now - _previousNetworkAt).TotalSeconds);
        var received = _previousNetwork is { } oldNetwork
            ? Math.Max(0, (long)((network.Received - oldNetwork.Received) / seconds)) : 0;
        var sent = _previousNetwork is { } oldNetwork2
            ? Math.Max(0, (long)((network.Sent - oldNetwork2.Sent) / seconds)) : 0;
        var snapshot = new SystemMetricsSnapshot(
            CpuPercent(cpu), memory.TotalPhysical - memory.AvailablePhysical, memory.TotalPhysical,
            received, sent, ReadVolumes());
        _previousCpu = cpu;
        _previousNetwork = network;
        _previousNetworkAt = now;
        Current = snapshot;
        Changed?.Invoke(snapshot);
    }

    private double CpuPercent(CpuTimes current)
    {
        if (_previousCpu is not { } previous) return 0;
        var total = current.Kernel + current.User - previous.Kernel - previous.User;
        var idle = current.Idle - previous.Idle;
        return total == 0 ? 0 : Math.Clamp((total - idle) * 100d / total, 0, 100);
    }

    private static CpuTimes ReadCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            throw new InvalidOperationException("GetSystemTimes failed.");
        return new CpuTimes(ToUInt64(idle), ToUInt64(kernel), ToUInt64(user));
    }

    private static MemoryStatus ReadMemory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status)) throw new InvalidOperationException("GlobalMemoryStatusEx failed.");
        return status;
    }

    private static NetworkTotals ReadNetwork()
    {
        ulong received = 0, sent = 0;
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                adapter.OperationalStatus != OperationalStatus.Up) continue;
            var statistics = adapter.GetIPStatistics();
            received += (ulong)Math.Max(0, statistics.BytesReceived);
            sent += (ulong)Math.Max(0, statistics.BytesSent);
        }
        return new NetworkTotals(received, sent);
    }

    private static IReadOnlyList<VolumeSnapshot> ReadVolumes() => DriveInfo.GetDrives()
        .Where(drive => drive.IsReady)
        .Select(drive => new VolumeSnapshot(drive.VolumeLabel, drive.RootDirectory.FullName,
            drive.TotalSize, drive.AvailableFreeSpace)).ToArray();

    private static ulong ToUInt64(FileTime time) => ((ulong)time.High << 32) | time.Low;
    private readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);
    private readonly record struct NetworkTotals(ulong Received, ulong Sent);

    [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint Low; public uint High; }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    {
        public uint Length; public uint MemoryLoad; public ulong TotalPhysical; public ulong AvailablePhysical;
        public ulong TotalPageFile; public ulong AvailablePageFile; public ulong TotalVirtual;
        public ulong AvailableVirtual; public ulong AvailableExtendedVirtual;
    }
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetSystemTimes(
        out FileTime idle, out FileTime kernel, out FileTime user);
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
