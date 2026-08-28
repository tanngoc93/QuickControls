using System;
using System.Runtime.InteropServices;

namespace QuickControls.Services
{
    public interface IAudioService : IDisposable
    {
        AudioState GetState();
        bool SetVolume(int percent);
        bool ToggleMute();
    }

    public sealed class AudioState
    {
        public AudioState(bool available, int volume, bool muted)
        {
            Available = available;
            Volume = volume;
            Muted = muted;
        }

        public bool Available { get; private set; }
        public int Volume { get; private set; }
        public bool Muted { get; private set; }
    }

    public sealed class AudioService : IAudioService
    {
        private readonly object _sync = new object();
        private readonly Guid _eventContext = new Guid("9FA11D69-B576-4255-A9B8-42F2364118D1");
        private IMMDeviceEnumerator _enumerator;
        private IMMDevice _device;
        private IAudioEndpointVolume _endpoint;
        private string _deviceId;
        private bool _disposed;

        public AudioState GetState()
        {
            lock (_sync)
            {
                try
                {
                    if (!EnsureEndpoint()) return new AudioState(false, 0, false);
                    float scalar;
                    bool muted;
                    if (_endpoint.GetMasterVolumeLevelScalar(out scalar) < 0 || _endpoint.GetMute(out muted) < 0)
                    {
                        ResetEndpoint();
                        return new AudioState(false, 0, false);
                    }
                    int volume = Clamp((int)Math.Round(scalar * 100F));
                    return new AudioState(true, volume, muted);
                }
                catch
                {
                    ResetEndpoint();
                    return new AudioState(false, 0, false);
                }
            }
        }

        public bool SetVolume(int percent)
        {
            lock (_sync)
            {
                try
                {
                    if (!EnsureEndpoint()) return false;
                    int clamped = Clamp(percent);
                    Guid context = _eventContext;
                    int result = _endpoint.SetMasterVolumeLevelScalar(clamped / 100F, ref context);
                    if (result >= 0 && clamped > 0)
                    {
                        _endpoint.SetMute(false, ref context);
                    }
                    return result >= 0;
                }
                catch
                {
                    ResetEndpoint();
                    return false;
                }
            }
        }

        public bool ToggleMute()
        {
            lock (_sync)
            {
                try
                {
                    if (!EnsureEndpoint()) return false;
                    bool muted;
                    if (_endpoint.GetMute(out muted) < 0) return false;
                    Guid context = _eventContext;
                    return _endpoint.SetMute(!muted, ref context) >= 0;
                }
                catch
                {
                    ResetEndpoint();
                    return false;
                }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                ResetEndpoint();
                ReleaseComObject(_enumerator);
                _enumerator = null;
            }
        }

        private bool EnsureEndpoint()
        {
            if (_disposed) return false;
            if (_enumerator == null)
            {
                _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            }

            IMMDevice currentDevice;
            if (_enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Console, out currentDevice) < 0 || currentDevice == null)
            {
                return false;
            }

            string currentId;
            if (currentDevice.GetId(out currentId) < 0)
            {
                ReleaseComObject(currentDevice);
                return false;
            }

            if (_endpoint != null && string.Equals(_deviceId, currentId, StringComparison.Ordinal))
            {
                ReleaseComObject(currentDevice);
                return true;
            }

            ResetEndpoint();
            object endpointObject;
            Guid interfaceId = typeof(IAudioEndpointVolume).GUID;
            int activateResult = currentDevice.Activate(ref interfaceId, ClsCtx.All, IntPtr.Zero, out endpointObject);
            if (activateResult < 0 || endpointObject == null)
            {
                ReleaseComObject(currentDevice);
                return false;
            }

            _device = currentDevice;
            _deviceId = currentId;
            _endpoint = (IAudioEndpointVolume)endpointObject;
            return true;
        }

        private void ResetEndpoint()
        {
            ReleaseComObject(_endpoint);
            ReleaseComObject(_device);
            _endpoint = null;
            _device = null;
            _deviceId = null;
        }

        private static int Clamp(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                try { Marshal.FinalReleaseComObject(value); }
                catch { }
            }
        }

        private enum EDataFlow
        {
            Render,
            Capture,
            All
        }

        private enum ERole
        {
            Console,
            Multimedia,
            Communications
        }

        [Flags]
        private enum ClsCtx : uint
        {
            InprocServer = 0x1,
            InprocHandler = 0x2,
            LocalServer = 0x4,
            RemoteServer = 0x10,
            All = InprocServer | InprocHandler | LocalServer | RemoteServer
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject
        {
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);
            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
            [PreserveSig]
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            [PreserveSig]
            int RegisterEndpointNotificationCallback(IntPtr client);
            [PreserveSig]
            int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid interfaceId, ClsCtx classContext, IntPtr activationParameters,
                [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
            [PreserveSig]
            int OpenPropertyStore(uint access, out IntPtr properties);
            [PreserveSig]
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig]
            int GetState(out uint state);
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out uint channelCount);
            [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
            [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
            [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
            [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);
            [PreserveSig] int VolumeStepUp(ref Guid eventContext);
            [PreserveSig] int VolumeStepDown(ref Guid eventContext);
            [PreserveSig] int QueryHardwareSupport(out uint hardwareSupportMask);
            [PreserveSig] int GetVolumeRange(out float minDb, out float maxDb, out float incrementDb);
        }
    }
}
