using System.Diagnostics;
using LibreHardwareMonitor.Hardware;
using InventariAgentSvc.Config;
using InventariAgentSvc.Models;

namespace InventariAgentSvc.Services;

public class MetricsCollector : IDisposable
{
    private readonly Computer _computer;
    private readonly ILogger<MetricsCollector> _logger;
    private readonly AgentConfig _config;
    private bool _disposed;

    public MetricsCollector(ILogger<MetricsCollector> logger, ConfigStore configStore)
    {
        _logger = logger;
        _config = configStore.Config;

        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsControllerEnabled = true,
            IsNetworkEnabled = false,
            IsMotherboardEnabled = true
        };

        _computer.Open();
        _computer.Accept(new UpdateVisitor());
    }

    public async Task<MetricsSnapshot> CaptureAsync()
    {
        _computer.Accept(new UpdateVisitor());

        var metrics = new MetricsSnapshot
        {
            CpuTempC = GetCpuTemperature(),
            GpuTempC = GetGpuTemperature(),
            CpuUsagePct = await GetCpuUsageAsync(),
            RamUsagePct = GetRamUsage(),
            DiskFreePct = GetDiskFreeSpace()
        };

        return metrics;
    }

    public bool IsCritical(MetricsSnapshot metrics)
    {
        if (metrics.CpuTempC >= _config.Thresholds.CpuTempCrit)
        {
            _logger.LogWarning("CPU temperature critical: {Temp}°C", metrics.CpuTempC);
            return true;
        }

        if (metrics.GpuTempC >= _config.Thresholds.GpuTempCrit)
        {
            _logger.LogWarning("GPU temperature critical: {Temp}°C", metrics.GpuTempC);
            return true;
        }

        if (metrics.CpuUsagePct >= _config.Thresholds.CpuUsageCrit)
        {
            _logger.LogWarning("CPU usage critical: {Usage}%", metrics.CpuUsagePct);
            return true;
        }

        return false;
    }

    private double GetCpuTemperature()
    {
        try
        {
            double maxTemp = 0;
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    hardware.Update(); // Actualizar lecturas del CPU
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            var temp = sensor.Value ?? 0;
                            if (temp > maxTemp)
                            {
                                maxTemp = temp;
                                _logger.LogDebug("CPU temp sensor: {Name} = {Temp}°C", sensor.Name, temp);
                            }
                        }
                    }
                }
            }
            if (maxTemp > 0)
            {
                return maxTemp;
            }
            _logger.LogWarning("No se encontraron sensores de temperatura del CPU");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo temperatura del CPU");
        }

        return 0;
    }

    private double GetGpuTemperature()
    {
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.GpuNvidia || 
                    hardware.HardwareType == HardwareType.GpuAmd)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            return sensor.Value ?? 0;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting GPU temperature");
        }

        return 0;
    }

    private async Task<double> GetCpuUsageAsync()
    {
        try
        {
            using var cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpu.NextValue(); // First call will always return 0
            await Task.Delay(1000); // Wait for a second reading
            return cpu.NextValue();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting CPU usage");
            return 0;
        }
    }

    private double GetRamUsage()
    {
        try
        {
            using var ram = new PerformanceCounter("Memory", "% Committed Bytes In Use");
            return ram.NextValue();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting RAM usage");
            return 0;
        }
    }

    private double GetDiskFreeSpace()
    {
        try
        {
            var drive = DriveInfo.GetDrives().First(d => d.IsReady && d.Name == "C:\\");
            var totalSize = drive.TotalSize;
            var freeSpace = drive.AvailableFreeSpace;
            return (double)freeSpace / totalSize * 100;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting disk free space");
            return 0;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _computer.Close();
            _disposed = true;
        }
    }
}

public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer)
    {
        computer.Traverse(this);
    }

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
    }

    public void VisitSensor(ISensor sensor) { }

    public void VisitParameter(IParameter parameter) { }
}