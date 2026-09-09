using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;

namespace Glass.Platform.Windows.Audio;

public sealed record AudioEndpointSnapshot(
    string EndpointId,
    string DisplayName,
    double Volume,
    bool IsMuted,
    bool IsAvailable);

public sealed class AudioEndpointService : IDisposable
{
    private readonly EndpointVolumeCallback _volumeCallback;
    private readonly EndpointNotificationCallback _deviceCallback;
    private readonly SynchronizationContext? _context;
    private IMMDeviceEnumerator? _enumerator;
    private IAudioEndpointVolume? _volume;
    private bool _started;
    private bool _disposed;

    public AudioEndpointService()
    {
        _context = SynchronizationContext.Current;
        _volumeCallback = new EndpointVolumeCallback(PublishFromCallback);
        _deviceCallback = new EndpointNotificationCallback(RequestDefaultEndpointRefresh);
    }

    public AudioEndpointSnapshot Current { get; private set; } =
        new(string.Empty, string.Empty, 0, false, false);
    public event Action<AudioEndpointSnapshot>? Changed;

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;
        cancellationToken.ThrowIfCancellationRequested();
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        _enumerator.RegisterEndpointNotificationCallback(_deviceCallback);
        await BindDefaultEndpointAsync(cancellationToken);
        _started = true;
    }

    private async ValueTask BindDefaultEndpointAsync(CancellationToken cancellationToken)
    {
        if (_enumerator is null) return;
        ReleaseVolume();
        _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var device);
        try
        {
        var endpointId = device.GetId();
        var iid = typeof(IAudioEndpointVolume).GUID;
        device.Activate(ref iid, 23, 0, out var volumeObject);
        _volume = (IAudioEndpointVolume)volumeObject;
        _volume.RegisterControlChangeNotify(_volumeCallback);
        var information = await DeviceInformation.CreateFromIdAsync(endpointId);
        Refresh(endpointId, information.Name);
        }
        finally
        {
            Marshal.FinalReleaseComObject(device);
        }
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stop();
        return ValueTask.CompletedTask;
    }

    public void SetVolume(double volume)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _volume?.SetMasterVolumeLevelScalar((float)Math.Clamp(volume, 0, 1), Guid.Empty);
    }

    public void SetMuted(bool muted)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _volume?.SetMute(muted, Guid.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        Changed = null;
    }

    private void Stop()
    {
        ReleaseVolume();
        if (_enumerator is not null)
        {
            try { _enumerator.UnregisterEndpointNotificationCallback(_deviceCallback); }
            catch (COMException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Audio endpoint callback removal failed: {exception.Message}");
            }
            Marshal.FinalReleaseComObject(_enumerator);
            _enumerator = null;
        }
        _started = false;
        Current = Current with { IsAvailable = false };
    }

    private void ReleaseVolume()
    {
        if (_volume is null) return;
        try { _volume.UnregisterControlChangeNotify(_volumeCallback); }
        catch (COMException exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Audio volume callback removal failed: {exception.Message}");
        }
        Marshal.FinalReleaseComObject(_volume);
        _volume = null;
    }

    private void RequestDefaultEndpointRefresh()
    {
        if (_disposed || !_started) return;
        void RefreshEndpoint(object? state) => _ = RefreshDefaultEndpointObservedAsync();
        if (_context is null) RefreshEndpoint(null);
        else _context.Post(RefreshEndpoint, null);
    }

    private async Task RefreshDefaultEndpointObservedAsync()
    {
        try { await BindDefaultEndpointAsync(CancellationToken.None); }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException)
        {
            Current = Current with { IsAvailable = false };
            Changed?.Invoke(Current);
            System.Diagnostics.Debug.WriteLine($"Audio endpoint refresh failed: {exception}");
        }
    }

    private void Refresh(string endpointId, string displayName)
    {
        if (_volume is null) return;
        _volume.GetMasterVolumeLevelScalar(out var scalar);
        _volume.GetMute(out var muted);
        Current = new AudioEndpointSnapshot(endpointId, displayName, scalar, muted, true);
        Changed?.Invoke(Current);
    }

    private void PublishFromCallback(float volume, bool muted)
    {
        if (_disposed || !_started) return;
        Current = Current with { Volume = volume, IsMuted = muted, IsAvailable = true };
        Changed?.Invoke(Current);
    }

    private sealed class EndpointVolumeCallback(Action<float, bool> publish) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(nint notificationData)
        {
            if (notificationData == 0) return 0;
            var data = Marshal.PtrToStructure<AudioVolumeNotification>(notificationData);
            publish(data.MasterVolume, data.Muted);
            return 0;
        }
    }

    private sealed class EndpointNotificationCallback(Action refresh) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string deviceId, uint newState) => 0;
        public int OnDeviceAdded(string deviceId) => 0;
        public int OnDeviceRemoved(string deviceId) => 0;
        public int OnDefaultDeviceChanged(DataFlow flow, Role role, string? deviceId)
        {
            if (flow == DataFlow.Render && role is Role.Multimedia or Role.Console) refresh();
            return 0;
        }
        public int OnPropertyValueChanged(string deviceId, PropertyKey key) => 0;
    }

    private enum DataFlow { Render, Capture, All }
    private enum Role { Console, Multimedia, Communications }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(DataFlow dataFlow, uint stateMask, out nint devices);
        void GetDefaultAudioEndpoint(DataFlow dataFlow, Role role, out IMMDevice endpoint);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice endpoint);
        void RegisterEndpointNotificationCallback(IMMNotificationClient callback);
        void UnregisterEndpointNotificationCallback(IMMNotificationClient callback);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(ref Guid interfaceId, uint classContext, nint activationParameters,
            [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
        void OpenPropertyStore();
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetId();
        void GetState();
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        void RegisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
        void UnregisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
        void GetChannelCount(out uint count);
        void SetMasterVolumeLevel(float level, Guid context);
        void SetMasterVolumeLevelScalar(float level, Guid context);
        void GetMasterVolumeLevel(out float level);
        void GetMasterVolumeLevelScalar(out float level);
        void SetChannelVolumeLevel(uint channel, float level, Guid context);
        void SetChannelVolumeLevelScalar(uint channel, float level, Guid context);
        void GetChannelVolumeLevel(uint channel, out float level);
        void GetChannelVolumeLevelScalar(uint channel, out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid context);
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        void GetVolumeStepInfo(out uint step, out uint stepCount);
        void VolumeStepUp(Guid context);
        void VolumeStepDown(Guid context);
        void QueryHardwareSupport(out uint mask);
        void GetVolumeRange(out float minimum, out float maximum, out float increment);
    }

    [ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(nint notificationData);
    }

    [ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMNotificationClient
    {
        [PreserveSig] int OnDeviceStateChanged(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId, uint newState);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
        [PreserveSig] int OnDefaultDeviceChanged(DataFlow flow, Role role,
            [MarshalAs(UnmanagedType.LPWStr)] string? deviceId);
        [PreserveSig] int OnPropertyValueChanged(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId, PropertyKey key);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PropertyKey
    {
        public readonly Guid FormatId;
        public readonly uint PropertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioVolumeNotification
    {
        public Guid EventContext;
        [MarshalAs(UnmanagedType.Bool)] public bool Muted;
        public float MasterVolume;
        public uint ChannelCount;
    }
}
