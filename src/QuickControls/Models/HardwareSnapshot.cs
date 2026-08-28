using System;

namespace QuickControls.Models
{
    public sealed class HardwareMetricReading
    {
        public HardwareMetricReading(
            string name,
            double? usagePercent,
            double? temperatureCelsius,
            long? usedBytes,
            long? totalBytes,
            bool present)
        {
            Name = name ?? string.Empty;
            UsagePercent = NormalizePercent(usagePercent);
            TemperatureCelsius = NormalizeTemperature(temperatureCelsius);
            UsedBytes = usedBytes.HasValue && usedBytes.Value >= 0 ? usedBytes : null;
            TotalBytes = totalBytes.HasValue && totalBytes.Value > 0 ? totalBytes : null;
            Present = present;
        }

        public string Name { get; private set; }
        public double? UsagePercent { get; private set; }
        public double? TemperatureCelsius { get; private set; }
        public long? UsedBytes { get; private set; }
        public long? TotalBytes { get; private set; }
        public bool Present { get; private set; }

        private static double? NormalizePercent(double? value)
        {
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value)) return null;
            return Math.Max(0D, Math.Min(100D, value.Value));
        }

        private static double? NormalizeTemperature(double? value)
        {
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value)) return null;
            return value.Value >= -20D && value.Value <= 150D ? value : null;
        }
    }

    public sealed class HardwareSnapshot
    {
        public HardwareSnapshot(
            DateTime sampledAt,
            HardwareMetricReading cpu,
            HardwareMetricReading gpu,
            HardwareMetricReading memory,
            HardwareMetricReading storage,
            string sensorProvider,
            bool temperatureAccessMayBeLimited)
        {
            SampledAt = sampledAt;
            Cpu = cpu ?? EmptyMetric();
            Gpu = gpu ?? EmptyMetric();
            Memory = memory ?? EmptyMetric();
            Storage = storage ?? EmptyMetric();
            SensorProvider = sensorProvider ?? string.Empty;
            TemperatureAccessMayBeLimited = temperatureAccessMayBeLimited;
        }

        public DateTime SampledAt { get; private set; }
        public HardwareMetricReading Cpu { get; private set; }
        public HardwareMetricReading Gpu { get; private set; }
        public HardwareMetricReading Memory { get; private set; }
        public HardwareMetricReading Storage { get; private set; }
        public string SensorProvider { get; private set; }
        public bool TemperatureAccessMayBeLimited { get; private set; }

        public static HardwareSnapshot Empty()
        {
            return new HardwareSnapshot(
                DateTime.Now,
                EmptyMetric(),
                EmptyMetric(),
                EmptyMetric(),
                EmptyMetric(),
                string.Empty,
                false);
        }

        private static HardwareMetricReading EmptyMetric()
        {
            return new HardwareMetricReading(string.Empty, null, null, null, null, false);
        }
    }
}
