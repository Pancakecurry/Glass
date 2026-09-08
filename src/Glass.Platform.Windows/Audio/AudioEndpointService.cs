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
    private IMMDeviceEnumerator? _enumerator;
    private IAudioEndpointVolume? _volume;
    private bool _started;
    private bool _disposed;

    public AudioEndpointService() => _volumeCallback = new EndpointVolumeCallback(PublishFromCallback);

    public AudioEndpointSnapshot Current { get; private set; } =
        new(string.Empty, string.Empty, 0, false, false);
    public event Action<AudioEndpointSnapshot>? Changed;

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;
        cancellationToken.ThrowIfCancellationRequested();
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var device);
        var endpointId = device.GetId();
        var iid = typeof(IAudioEndpointVolume).GUID;
        device.Activate(ref iid, 23, 0, out var volumeObject);
        _volume = (IAudioEndpointVolume)volumeObject;
        _volume.RegisterControlChangeNotify(_volumeCallback);
        var information = await DeviceInformation.CreateFromIdAsync(endpointId);
        _started = true;
        Refresh(endpointId, information.Name);
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
        if (_volume is not null)
        {
            try { _volume.UnregisterControlChangeNotify(_volumeCallback); }
            catch (COMException) { }
            Marshal.FinalReleaseComObject(_volume);
            _volume = null;
        }
        if (_enumerator is not null)
        {
            Marshal.FinalReleaseComObject(_enumerator);
            _enumerator = null;
        }
        _started = false;
        Current = Current with { IsAvailable = false };
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

    private enum DataFlow { Render, Capture, All }
    private enum Role { Console, Multimedia, Communications }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints();
        void GetDefaultAudioEndpoint(DataFlow dataFlow, Role role, out IMMDevice endpoint);
        void GetDevice();
        void RegisterEndpointNotificationCallback();
        void UnregisterEndpointNotificationCallback();
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

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioVolumeNotification
    {
        public Guid EventContext;
        [MarshalAs(UnmanagedType.Bool)] public bool Muted;
        public float MasterVolume;
        public uint ChannelCount;
    }
}
