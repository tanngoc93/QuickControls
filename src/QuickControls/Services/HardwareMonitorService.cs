using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using QuickControls.Models;

namespace QuickControls.Services
{
    public interface IHardwareMonitorService : IDisposable
    {
        HardwareSnapshot ReadSnapshot();
    }

    public sealed class HardwareMonitorService : IHardwareMonitorService
    {
        private readonly object _sync = new object();
        private bool _namesLoaded;
        private string _cpuName = string.Empty;
        private string _gpuName = string.Empty;
        private string _storageName = string.Empty;
        private double? _initialCpuUsage;
        private bool _hasCpuTimes;
        private ulong _previousIdle;
        private ulong _previousKernel;
        private ulong _previousUser;
        private DateTime _nextGpuTemperatureRead = DateTime.MinValue;
        private DateTime _nextStorageTemperatureRead = DateTime.MinValue;
        private double? _gpuTemperature;
        private double? _storageTemperature;
        private PerformanceCounterCategory _gpuCounterCategory;
        private readonly Dictionary<string, GpuPerformanceCounter> _gpuCounters =
            new Dictionary<string, GpuPerformanceCounter>(StringComparer.OrdinalIgnoreCase);
        private DateTime _nextGpuCounterRefresh = DateTime.MinValue;
        private bool _gpuCountersUnavailable;
        private PerformanceCounterCategory _storageCounterCategory;
        private readonly Dictionary<string, PerformanceCounter> _storageCounters =
            new Dictionary<string, PerformanceCounter>(StringComparer.OrdinalIgnoreCase);
        private DateTime _nextStorageCounterRefresh = DateTime.MinValue;
        private bool _storageCountersUnavailable;
        private bool _disposed;

        public HardwareSnapshot ReadSnapshot()
        {
            lock (_sync)
            {
                if (_disposed) return HardwareSnapshot.Empty();
                EnsureDeviceNames();

                DateTime now = DateTime.Now;
                double? cpuUsage = ReadCpuUsage();
                MemoryReading memory = ReadMemory();
                double? gpuUsage = ReadGpuUsage();
                double? storageUsage = ReadStorageUsage();

                if (now >= _nextGpuTemperatureRead)
                {
                    _gpuTemperature = WindowsTemperatureReader.ReadGpuTemperature();
                    _nextGpuTemperatureRead = now.AddSeconds(3D);
                }
                if (now >= _nextStorageTemperatureRead)
                {
                    _storageTemperature = WindowsTemperatureReader.ReadStorageTemperature();
                    _nextStorageTemperatureRead = now.AddSeconds(20D);
                }

                bool gpuPresent = !string.IsNullOrEmpty(_gpuName) || gpuUsage.HasValue ||
                    _gpuTemperature.HasValue;
                bool storagePresent = !string.IsNullOrEmpty(_storageName) || storageUsage.HasValue ||
                    _storageTemperature.HasValue;
                HardwareMetricReading cpu = new HardwareMetricReading(
                    // Windows has no reliable, general CPU package-temperature API.
                    _cpuName, cpuUsage, null, null, null, !string.IsNullOrEmpty(_cpuName) || cpuUsage.HasValue);
                HardwareMetricReading gpu = new HardwareMetricReading(
                    _gpuName, gpuUsage, _gpuTemperature, null, null, gpuPresent);
                HardwareMetricReading ram = new HardwareMetricReading(
                    string.Empty, memory.UsagePercent, null, memory.UsedBytes, memory.TotalBytes,
                    memory.TotalBytes.HasValue);
                HardwareMetricReading storage = new HardwareMetricReading(
                    _storageName, storageUsage, _storageTemperature, null, null, storagePresent);

                return new HardwareSnapshot(
                    now,
                    cpu,
                    gpu,
                    ram,
                    storage,
                    "Windows",
                    !_gpuTemperature.HasValue && !_storageTemperature.HasValue);
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                DisposeGpuCounters();
                DisposeStorageCounters();
            }
        }

        private void EnsureDeviceNames()
        {
            if (_namesLoaded) return;
            _namesLoaded = true;
            _cpuName = ReadFirstName("SELECT Name, LoadPercentage FROM Win32_Processor", "Name", out _initialCpuUsage);
            _gpuName = ReadJoinedNames("SELECT Name FROM Win32_VideoController", "Name", 2);
            _storageName = ReadJoinedNames("SELECT Model FROM Win32_DiskDrive", "Model", 2);
        }

        private double? ReadCpuUsage()
        {
            FileTime idle;
            FileTime kernel;
            FileTime user;
            if (!GetSystemTimes(out idle, out kernel, out user)) return _initialCpuUsage;

            ulong idleValue = idle.ToUInt64();
            ulong kernelValue = kernel.ToUInt64();
            ulong userValue = user.ToUInt64();
            if (!_hasCpuTimes)
            {
                _hasCpuTimes = true;
                _previousIdle = idleValue;
                _previousKernel = kernelValue;
                _previousUser = userValue;
                double? first = _initialCpuUsage;
                _initialCpuUsage = null;
                return first;
            }

            ulong idleDelta = SafeDelta(idleValue, _previousIdle);
            ulong kernelDelta = SafeDelta(kernelValue, _previousKernel);
            ulong userDelta = SafeDelta(userValue, _previousUser);
            _previousIdle = idleValue;
            _previousKernel = kernelValue;
            _previousUser = userValue;
            ulong total = kernelDelta + userDelta;
            if (total == 0UL || idleDelta > total) return null;
            return 100D * (total - idleDelta) / total;
        }

        private static ulong SafeDelta(ulong current, ulong previous)
        {
            return current >= previous ? current - previous : 0UL;
        }

        private static MemoryReading ReadMemory()
        {
            MemoryStatus status = new MemoryStatus();
            status.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0UL)
                return new MemoryReading(null, null, null);
            ulong used = status.TotalPhysical - Math.Min(status.TotalPhysical, status.AvailablePhysical);
            long totalBytes = status.TotalPhysical > long.MaxValue ? long.MaxValue : (long)status.TotalPhysical;
            long usedBytes = used > long.MaxValue ? long.MaxValue : (long)used;
            return new MemoryReading(status.MemoryLoad, usedBytes, totalBytes);
        }

        private double? ReadGpuUsage()
        {
            if (!_gpuCountersUnavailable)
            {
                RefreshGpuCounters();
                if (!_gpuCountersUnavailable)
                {
                    Dictionary<string, double> engineTotals =
                        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    bool foundSample = false;
                    foreach (GpuPerformanceCounter item in _gpuCounters.Values)
                    {
                        try
                        {
                            double value = item.Counter.NextValue();
                            foundSample = true;
                            double existing;
                            engineTotals.TryGetValue(item.EngineKey, out existing);
                            engineTotals[item.EngineKey] = Math.Min(100D, existing + Math.Max(0D, value));
                        }
                        catch
                        {
                        }
                    }

                    if (foundSample)
                    {
                        double busiest = 0D;
                        foreach (double value in engineTotals.Values) busiest = Math.Max(busiest, value);
                        return busiest;
                    }
                }
            }
            return ReadGpuUsageFromWmi();
        }

        private void RefreshGpuCounters()
        {
            DateTime now = DateTime.UtcNow;
            if (now < _nextGpuCounterRefresh) return;
            _nextGpuCounterRefresh = now.AddSeconds(5D);
            try
            {
                if (_gpuCounterCategory == null)
                    _gpuCounterCategory = new PerformanceCounterCategory("GPU Engine");
                string[] instances = _gpuCounterCategory.GetInstanceNames();
                HashSet<string> active = new HashSet<string>(instances, StringComparer.OrdinalIgnoreCase);
                List<string> removed = new List<string>();
                foreach (KeyValuePair<string, GpuPerformanceCounter> pair in _gpuCounters)
                    if (!active.Contains(pair.Key)) removed.Add(pair.Key);
                for (int index = 0; index < removed.Count; index++)
                {
                    _gpuCounters[removed[index]].Counter.Dispose();
                    _gpuCounters.Remove(removed[index]);
                }

                for (int index = 0; index < instances.Length; index++)
                {
                    string instance = instances[index];
                    if (_gpuCounters.ContainsKey(instance)) continue;
                    PerformanceCounter counter = new PerformanceCounter(
                        "GPU Engine", "Utilization Percentage", instance, true);
                    try { counter.NextValue(); }
                    catch
                    {
                        counter.Dispose();
                        continue;
                    }
                    _gpuCounters[instance] = new GpuPerformanceCounter(counter, GetGpuEngineKey(instance));
                }
            }
            catch
            {
                _gpuCountersUnavailable = true;
                DisposeGpuCounters();
            }
        }

        private static string GetGpuEngineKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            int luidIndex = name.IndexOf("_luid_", StringComparison.OrdinalIgnoreCase);
            return luidIndex >= 0 ? name.Substring(luidIndex) : name;
        }

        private void DisposeGpuCounters()
        {
            foreach (GpuPerformanceCounter counter in _gpuCounters.Values) counter.Counter.Dispose();
            _gpuCounters.Clear();
            _gpuCounterCategory = null;
        }

        private static double? ReadGpuUsageFromWmi()
        {
            try
            {
                Dictionary<string, double> engineTotals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "root\\cimv2",
                    "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject item in results)
                    {
                        string name = Convert.ToString(item["Name"]);
                        double value;
                        if (!TryConvertDouble(item["UtilizationPercentage"], out value)) continue;
                        string engineKey = GetGpuEngineKey(name);
                        double existing;
                        engineTotals.TryGetValue(engineKey, out existing);
                        engineTotals[engineKey] = Math.Min(100D, existing + Math.Max(0D, value));
                    }
                }

                double busiest = 0D;
                bool found = false;
                foreach (double value in engineTotals.Values)
                {
                    busiest = Math.Max(busiest, value);
                    found = true;
                }
                return found ? (double?)busiest : null;
            }
            catch
            {
                return null;
            }
        }

        private double? ReadStorageUsage()
        {
            if (!_storageCountersUnavailable)
            {
                RefreshStorageCounters();
                if (!_storageCountersUnavailable && _storageCounters.Count > 0)
                {
                    double busiest = 0D;
                    bool found = false;
                    foreach (PerformanceCounter counter in _storageCounters.Values)
                    {
                        try
                        {
                            busiest = Math.Max(busiest, counter.NextValue());
                            found = true;
                        }
                        catch
                        {
                        }
                    }
                    if (found) return Math.Max(0D, Math.Min(100D, busiest));
                }
            }
            return ReadStorageUsageFromWmi();
        }

        private void RefreshStorageCounters()
        {
            DateTime now = DateTime.UtcNow;
            if (now < _nextStorageCounterRefresh) return;
            _nextStorageCounterRefresh = now.AddSeconds(5D);
            try
            {
                if (_storageCounterCategory == null)
                    _storageCounterCategory = new PerformanceCounterCategory("PhysicalDisk");
                string[] instances = _storageCounterCategory.GetInstanceNames();
                HashSet<string> active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < instances.Length; index++)
                {
                    string instance = instances[index];
                    if (string.Equals(instance, "_Total", StringComparison.OrdinalIgnoreCase)) continue;
                    active.Add(instance);
                    if (_storageCounters.ContainsKey(instance)) continue;
                    PerformanceCounter counter = null;
                    try
                    {
                        counter = new PerformanceCounter("PhysicalDisk", "% Disk Time", instance, true);
                        counter.NextValue();
                        _storageCounters[instance] = counter;
                        counter = null;
                    }
                    catch
                    {
                        if (counter != null) counter.Dispose();
                    }
                }

                List<string> removed = new List<string>();
                foreach (KeyValuePair<string, PerformanceCounter> pair in _storageCounters)
                    if (!active.Contains(pair.Key)) removed.Add(pair.Key);
                for (int index = 0; index < removed.Count; index++)
                {
                    _storageCounters[removed[index]].Dispose();
                    _storageCounters.Remove(removed[index]);
                }
            }
            catch
            {
                _storageCountersUnavailable = true;
                DisposeStorageCounters();
            }
        }

        private void DisposeStorageCounters()
        {
            foreach (PerformanceCounter counter in _storageCounters.Values) counter.Dispose();
            _storageCounters.Clear();
            _storageCounterCategory = null;
        }

        private static double? ReadStorageUsageFromWmi()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "root\\cimv2",
                    "SELECT Name, PercentDiskTime FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    double busiest = 0D;
                    bool found = false;
                    double? totalFallback = null;
                    foreach (ManagementObject item in results)
                    {
                        double value;
                        if (!TryConvertDouble(item["PercentDiskTime"], out value)) continue;
                        value = Math.Max(0D, Math.Min(100D, value));
                        if (string.Equals(Convert.ToString(item["Name"]), "_Total", StringComparison.OrdinalIgnoreCase))
                        {
                            totalFallback = value;
                        }
                        else
                        {
                            busiest = Math.Max(busiest, value);
                            found = true;
                        }
                    }
                    if (found) return busiest;
                    return totalFallback;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string ReadFirstName(string query, string nameProperty, out double? load)
        {
            load = null;
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\cimv2", query))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject item in results)
                    {
                        double value;
                        if (TryConvertDouble(item["LoadPercentage"], out value)) load = value;
                        string name = CleanName(Convert.ToString(item[nameProperty]));
                        if (!string.IsNullOrEmpty(name)) return name;
                    }
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        private static string ReadJoinedNames(string query, string property, int maximum)
        {
            List<string> names = new List<string>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\cimv2", query))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject item in results)
                    {
                        string name = CleanName(Convert.ToString(item[property]));
                        if (string.IsNullOrEmpty(name) || names.Contains(name)) continue;
                        names.Add(name);
                        if (names.Count >= maximum) break;
                    }
                }
            }
            catch
            {
            }
            return string.Join(" + ", names.ToArray());
        }

        private static string CleanName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string cleaned = value.Replace("(R)", string.Empty).Replace("(TM)", string.Empty).Trim();
            return cleaned.Length <= 64 ? cleaned : cleaned.Substring(0, 61) + "...";
        }

        private static bool TryConvertDouble(object value, out double result)
        {
            try
            {
                if (value != null)
                {
                    result = Convert.ToDouble(value);
                    return !double.IsNaN(result) && !double.IsInfinity(result);
                }
            }
            catch
            {
            }
            result = 0D;
            return false;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint Low;
            public uint High;
            public ulong ToUInt64() { return ((ulong)High << 32) | Low; }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MemoryStatus
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        private struct MemoryReading
        {
            public MemoryReading(double? usagePercent, long? usedBytes, long? totalBytes)
            {
                UsagePercent = usagePercent;
                UsedBytes = usedBytes;
                TotalBytes = totalBytes;
            }
            public double? UsagePercent;
            public long? UsedBytes;
            public long? TotalBytes;
        }

        private sealed class GpuPerformanceCounter
        {
            public GpuPerformanceCounter(PerformanceCounter counter, string engineKey)
            {
                Counter = counter;
                EngineKey = engineKey;
            }
            public PerformanceCounter Counter;
            public string EngineKey;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);
    }

    internal static class WindowsTemperatureReader
    {
        private const int AdapterPerformanceData = 62;
        private const uint StorageDeviceTemperatureProperty = 52U;
        private const uint IoctlStorageQueryProperty = 0x002D1400U;
        private const uint ShareRead = 0x00000001U;
        private const uint ShareWrite = 0x00000002U;
        private const uint OpenExisting = 3U;

        public static double? ReadGpuTemperature()
        {
            if (Environment.OSVersion.Version < new Version(10, 0, 17134)) return null;
            IntPtr adapterBuffer = IntPtr.Zero;
            try
            {
                EnumAdapters2 enumeration = new EnumAdapters2();
                int status = D3DKMTEnumAdapters2(ref enumeration);
                if (status != 0 || enumeration.NumAdapters == 0U || enumeration.NumAdapters > 64U) return null;

                int adapterSize = Marshal.SizeOf(typeof(AdapterInfo));
                adapterBuffer = Marshal.AllocHGlobal(adapterSize * (int)enumeration.NumAdapters);
                enumeration.Adapters = adapterBuffer;
                status = D3DKMTEnumAdapters2(ref enumeration);
                if (status != 0) return null;

                double? hottest = null;
                for (int index = 0; index < (int)enumeration.NumAdapters; index++)
                {
                    AdapterInfo adapter = (AdapterInfo)Marshal.PtrToStructure(
                        IntPtr.Add(adapterBuffer, index * adapterSize), typeof(AdapterInfo));
                    try
                    {
                        AdapterPerformance performance = new AdapterPerformance();
                        performance.PhysicalAdapterIndex = 0U;
                        IntPtr performanceBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(AdapterPerformance)));
                        try
                        {
                            Marshal.StructureToPtr(performance, performanceBuffer, false);
                            QueryAdapterInfo query = new QueryAdapterInfo();
                            query.Adapter = adapter.Adapter;
                            query.Type = AdapterPerformanceData;
                            query.PrivateDriverData = performanceBuffer;
                            query.PrivateDriverDataSize = (uint)Marshal.SizeOf(typeof(AdapterPerformance));
                            if (D3DKMTQueryAdapterInfo(ref query) != 0) continue;
                            performance = (AdapterPerformance)Marshal.PtrToStructure(
                                performanceBuffer, typeof(AdapterPerformance));
                            // D3DKMT_ADAPTER_PERFDATA reports tenths of a degree Celsius.
                            double temperature = performance.Temperature / 10D;
                            if (temperature < 1D || temperature > 150D) continue;
                            hottest = hottest.HasValue ? Math.Max(hottest.Value, temperature) : temperature;
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(performanceBuffer);
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        CloseAdapter close = new CloseAdapter();
                        close.Adapter = adapter.Adapter;
                        try { D3DKMTCloseAdapter(ref close); }
                        catch { }
                    }
                }
                return hottest;
            }
            catch (DllNotFoundException)
            {
                return null;
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (adapterBuffer != IntPtr.Zero) Marshal.FreeHGlobal(adapterBuffer);
            }
        }

        public static double? ReadStorageTemperature()
        {
            double? hottest = null;
            List<int> driveIndexes = ReadPhysicalDriveIndexes();
            for (int drive = 0; drive < driveIndexes.Count; drive++)
            {
                int index = driveIndexes[drive];
                string path = "\\\\.\\PhysicalDrive" + index;
                using (SafeFileHandle handle = CreateFile(
                    path, 0U, ShareRead | ShareWrite, IntPtr.Zero, OpenExisting, 0U, IntPtr.Zero))
                {
                    if (handle.IsInvalid)
                    {
                        continue;
                    }

                    byte[] query = new byte[12];
                    WriteUInt32(query, 0, StorageDeviceTemperatureProperty);
                    byte[] output = new byte[1024];
                    uint returned;
                    if (!DeviceIoControl(handle, IoctlStorageQueryProperty, query, (uint)query.Length,
                        output, (uint)output.Length, out returned, IntPtr.Zero) || returned < 40U)
                        continue;

                    // The descriptor header is 24 bytes; each temperature record is 16 bytes.
                    int infoCount = ReadUInt16(output, 12);
                    int availableCount = Math.Min(infoCount, (int)(returned - 24U) / 16);
                    for (int sensor = 0; sensor < availableCount; sensor++)
                    {
                        int offset = 24 + sensor * 16;
                        short temperature = ReadInt16(output, offset + 2);
                        if (temperature < -20 || temperature > 150) continue;
                        hottest = hottest.HasValue ? Math.Max(hottest.Value, temperature) : temperature;
                    }
                }
            }
            return hottest;
        }

        private static List<int> ReadPhysicalDriveIndexes()
        {
            List<int> indexes = new List<int>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "root\\cimv2", "SELECT Index FROM Win32_DiskDrive"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject item in results)
                    {
                        int index = Convert.ToInt32(item["Index"]);
                        if (index >= 0 && index <= 255 && !indexes.Contains(index)) indexes.Add(index);
                    }
                }
            }
            catch
            {
            }
            if (indexes.Count == 0)
            {
                // A bounded fallback covers ordinary workstations and sparse high-number drive IDs.
                for (int index = 0; index < 32; index++) indexes.Add(index);
            }
            return indexes;
        }

        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static int ReadUInt16(byte[] buffer, int offset)
        {
            return buffer[offset] | (buffer[offset + 1] << 8);
        }

        private static short ReadInt16(byte[] buffer, int offset)
        {
            return unchecked((short)ReadUInt16(buffer, offset));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AdapterInfo
        {
            public uint Adapter;
            public Luid AdapterLuid;
            public uint NumSources;
            public int PrecisePresentRegionsPreferred;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct EnumAdapters2
        {
            public uint NumAdapters;
            public IntPtr Adapters;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct QueryAdapterInfo
        {
            public uint Adapter;
            public int Type;
            public IntPtr PrivateDriverData;
            public uint PrivateDriverDataSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AdapterPerformance
        {
            public uint PhysicalAdapterIndex;
            public ulong MemoryFrequency;
            public ulong MaxMemoryFrequency;
            public ulong MaxMemoryFrequencyOverclocked;
            public ulong MemoryBandwidth;
            public ulong PcieBandwidth;
            public uint FanRpm;
            public uint Power;
            public uint Temperature;
            public byte PowerStateOverride;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CloseAdapter
        {
            public uint Adapter;
        }

        [DllImport("gdi32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern int D3DKMTEnumAdapters2(ref EnumAdapters2 enumeration);

        [DllImport("gdi32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern int D3DKMTQueryAdapterInfo(ref QueryAdapterInfo query);

        [DllImport("gdi32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern int D3DKMTCloseAdapter(ref CloseAdapter close);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle device,
            uint controlCode,
            byte[] input,
            uint inputSize,
            byte[] output,
            uint outputSize,
            out uint bytesReturned,
            IntPtr overlapped);
    }
}
