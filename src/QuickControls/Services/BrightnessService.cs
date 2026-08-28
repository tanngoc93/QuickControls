using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;

namespace QuickControls.Services
{
    public interface IBrightnessService : IDisposable
    {
        IList<BrightnessDevice> Devices { get; }
        string StatusMessage { get; }
        void Refresh();
    }

    public abstract class BrightnessDevice : IDisposable
    {
        protected BrightnessDevice(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public abstract bool TryGetPercent(out int percent);
        public abstract bool SetPercent(int percent);
        public abstract void Dispose();
        public override string ToString() { return DisplayName; }

        protected static int Clamp(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }
    }

    public sealed class BrightnessService : IBrightnessService
    {
        private readonly List<BrightnessDevice> _devices;
        private bool _disposed;

        public BrightnessService()
            : this(true)
        {
        }

        public BrightnessService(bool refreshImmediately)
        {
            _devices = new List<BrightnessDevice>();
            StatusMessage = AppText.Get("Panel.DisplaySearching");
            if (refreshImmediately) Refresh();
        }

        public IList<BrightnessDevice> Devices
        {
            get { return _devices.AsReadOnly(); }
        }

        public string StatusMessage { get; private set; }

        public void Refresh()
        {
            if (_disposed) return;
            DisposeDevices();

            try
            {
                AddWmiDevices();
            }
            catch
            {
            }

            try
            {
                AddDdcDevices();
            }
            catch
            {
            }

            StatusMessage = _devices.Count == 0
                ? AppText.Get("Panel.DisplayUnsupported")
                : string.Empty;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisposeDevices();
        }

        private void AddWmiDevices()
        {
            ManagementScope scope = new ManagementScope(@"\\.\root\wmi");
            scope.Connect();
            List<WmiMonitorInfo> monitors = new List<WmiMonitorInfo>();
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                scope, new ObjectQuery("SELECT InstanceName, CurrentBrightness, Level, Levels, Active FROM WmiMonitorBrightness WHERE Active = TRUE")))
            using (ManagementObjectCollection results = searcher.Get())
            {
                foreach (ManagementObject item in results)
                {
                    string instanceName = Convert.ToString(item["InstanceName"]);
                    byte[] levels = item["Level"] as byte[];
                    if (!string.IsNullOrEmpty(instanceName))
                    {
                        monitors.Add(new WmiMonitorInfo(instanceName, levels));
                    }
                    item.Dispose();
                }
            }

            for (int index = 0; index < monitors.Count; index++)
            {
                string name = monitors.Count == 1
                    ? AppText.Get("Display.Laptop")
                    : AppText.Format("Display.LaptopNumber", index + 1);
                _devices.Add(new WmiBrightnessDevice(monitors[index].InstanceName, name, monitors[index].Levels));
            }
        }

        private void AddDdcDevices()
        {
            int externalNumber = 0;
            MonitorEnumDelegate callback = delegate(IntPtr monitor, IntPtr deviceContext, ref NativeRect monitorRect, IntPtr data)
            {
                string monitorIdentity = GetMonitorIdentity(monitor);
                uint count;
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out count) || count == 0)
                {
                    return true;
                }

                PhysicalMonitor[] physicalMonitors = new PhysicalMonitor[count];
                if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, physicalMonitors))
                {
                    return true;
                }

                for (int index = 0; index < physicalMonitors.Length; index++)
                {
                    uint minimum;
                    uint current;
                    uint maximum;
                    if (GetMonitorBrightness(physicalMonitors[index].Handle, out minimum, out current, out maximum) && maximum > minimum)
                    {
                        externalNumber++;
                        string description = CleanDescription(physicalMonitors[index].Description);
                        string displayName = string.IsNullOrEmpty(description)
                            ? AppText.Format("Display.ExternalNumber", externalNumber)
                            : description;
                        string id = !string.IsNullOrEmpty(monitorIdentity)
                            ? "ddc:" + monitorIdentity + ":" + index
                            : "ddc:" + displayName + ":" + externalNumber;
                        _devices.Add(new DdcBrightnessDevice(id, displayName, physicalMonitors[index].Handle));
                    }
                    else
                    {
                        DestroyPhysicalMonitor(physicalMonitors[index].Handle);
                    }
                }
                return true;
            };

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        }

        private static string GetMonitorIdentity(IntPtr monitor)
        {
            MonitorInfoEx monitorInfo = new MonitorInfoEx();
            monitorInfo.Size = Marshal.SizeOf(typeof(MonitorInfoEx));
            if (!GetMonitorInfo(monitor, ref monitorInfo) || string.IsNullOrEmpty(monitorInfo.DeviceName))
            {
                return string.Empty;
            }

            DisplayDevice displayDevice = new DisplayDevice();
            displayDevice.Size = Marshal.SizeOf(typeof(DisplayDevice));
            const uint getDeviceInterfaceName = 0x00000001;
            if (EnumDisplayDevices(monitorInfo.DeviceName, 0, ref displayDevice, getDeviceInterfaceName) &&
                !string.IsNullOrEmpty(displayDevice.DeviceId))
            {
                return displayDevice.DeviceId.Trim().Trim('\0');
            }

            return monitorInfo.DeviceName.Trim().Trim('\0');
        }

        private void DisposeDevices()
        {
            for (int index = 0; index < _devices.Count; index++)
            {
                try { _devices[index].Dispose(); }
                catch { }
            }
            _devices.Clear();
        }

        private static string CleanDescription(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string clean = value.Trim().Trim('\0');
            if (string.Equals(clean, "Generic PnP Monitor", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            return clean;
        }

        private sealed class WmiMonitorInfo
        {
            public WmiMonitorInfo(string instanceName, byte[] levels)
            {
                InstanceName = instanceName;
                Levels = levels;
            }
            public string InstanceName { get; private set; }
            public byte[] Levels { get; private set; }
        }

        private sealed class WmiBrightnessDevice : BrightnessDevice
        {
            private readonly string _instanceName;
            private readonly byte[] _levels;

            public WmiBrightnessDevice(string instanceName, string displayName, byte[] levels)
                : base("wmi:" + instanceName, displayName)
            {
                _instanceName = instanceName;
                _levels = levels;
            }

            public override bool TryGetPercent(out int percent)
            {
                percent = 0;
                try
                {
                    ManagementScope scope = new ManagementScope(@"\\.\root\wmi");
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                        scope, new ObjectQuery("SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness WHERE Active = TRUE")))
                    using (ManagementObjectCollection results = searcher.Get())
                    {
                        foreach (ManagementObject item in results)
                        {
                            bool match = string.Equals(Convert.ToString(item["InstanceName"]), _instanceName, StringComparison.OrdinalIgnoreCase);
                            if (match)
                            {
                                percent = Clamp(Convert.ToInt32(item["CurrentBrightness"]));
                                item.Dispose();
                                return true;
                            }
                            item.Dispose();
                        }
                    }
                }
                catch
                {
                }
                return false;
            }

            public override bool SetPercent(int percent)
            {
                try
                {
                    byte target = (byte)NearestSupportedLevel(Clamp(percent));
                    ManagementScope scope = new ManagementScope(@"\\.\root\wmi");
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                        scope, new ObjectQuery("SELECT * FROM WmiMonitorBrightnessMethods WHERE Active = TRUE")))
                    using (ManagementObjectCollection results = searcher.Get())
                    {
                        foreach (ManagementObject item in results)
                        {
                            bool match = string.Equals(Convert.ToString(item["InstanceName"]), _instanceName, StringComparison.OrdinalIgnoreCase);
                            if (match)
                            {
                                using (ManagementBaseObject parameters = item.GetMethodParameters("WmiSetBrightness"))
                                {
                                    parameters["Timeout"] = (uint)1;
                                    parameters["Brightness"] = target;
                                    using (ManagementBaseObject output = item.InvokeMethod("WmiSetBrightness", parameters, null))
                                    {
                                        object returnValue = output == null ? null : output["ReturnValue"];
                                        bool succeeded = returnValue == null || Convert.ToUInt32(returnValue) == 0U;
                                        item.Dispose();
                                        return succeeded;
                                    }
                                }
                            }
                            item.Dispose();
                        }
                    }
                }
                catch
                {
                }
                return false;
            }

            public override void Dispose()
            {
            }

            private int NearestSupportedLevel(int requested)
            {
                if (_levels == null || _levels.Length == 0) return requested;
                int closest = _levels[0];
                int distance = Math.Abs(closest - requested);
                for (int index = 1; index < _levels.Length; index++)
                {
                    int candidateDistance = Math.Abs(_levels[index] - requested);
                    if (candidateDistance < distance)
                    {
                        closest = _levels[index];
                        distance = candidateDistance;
                    }
                }
                return closest;
            }
        }

        private sealed class DdcBrightnessDevice : BrightnessDevice
        {
            private readonly object _sync = new object();
            private IntPtr _handle;
            private bool _disposed;

            public DdcBrightnessDevice(string id, string displayName, IntPtr handle)
                : base(id, displayName)
            {
                _handle = handle;
            }

            public override bool TryGetPercent(out int percent)
            {
                lock (_sync)
                {
                    percent = 0;
                    if (_disposed) return false;
                    uint minimum;
                    uint current;
                    uint maximum;
                    if (!GetMonitorBrightness(_handle, out minimum, out current, out maximum) || maximum <= minimum)
                    {
                        return false;
                    }
                    percent = Clamp((int)Math.Round((current - minimum) * 100D / (maximum - minimum)));
                    return true;
                }
            }

            public override bool SetPercent(int percent)
            {
                lock (_sync)
                {
                    if (_disposed) return false;
                    uint minimum;
                    uint current;
                    uint maximum;
                    if (!GetMonitorBrightness(_handle, out minimum, out current, out maximum) || maximum <= minimum)
                    {
                        return false;
                    }
                    uint target = minimum + (uint)Math.Round((maximum - minimum) * Clamp(percent) / 100D);
                    return SetMonitorBrightness(_handle, target);
                }
            }

            public override void Dispose()
            {
                lock (_sync)
                {
                    if (!_disposed)
                    {
                        DestroyPhysicalMonitor(_handle);
                        _disposed = true;
                        _handle = IntPtr.Zero;
                    }
                }
            }
        }

        private delegate bool MonitorEnumDelegate(IntPtr monitor, IntPtr deviceContext, ref NativeRect monitorRect, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MonitorInfoEx
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect WorkArea;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DisplayDevice
        {
            public int Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct PhysicalMonitor
        {
            public IntPtr Handle;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr deviceContext, IntPtr clipRect, MonitorEnumDelegate callback, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx monitorInfo);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumDisplayDevices(string device, uint deviceNumber, ref DisplayDevice displayDevice, uint flags);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);

        [DllImport("dxva2.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitor[] physicalMonitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetMonitorBrightness(IntPtr monitor, out uint minimum, out uint current, out uint maximum);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool SetMonitorBrightness(IntPtr monitor, uint brightness);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool DestroyPhysicalMonitor(IntPtr monitor);
    }
}
