using System.Diagnostics;
using System.Management;
using LibreHardwareMonitor.Hardware;
using InventariAgentSvc.Config;
using InventariAgentSvc.Models;

namespace InventariAgentSvc.Services;

public class ProcessSnapshotper
{
    private readonly ILogger<ProcessSnapshotper> _logger;

    public ProcessSnapshotper(ILogger<ProcessSnapshotper> logger)
    {
        _logger = logger;
    }

    public async Task<List<ProcSample>> CaptureTopAsync(int topN)
    {
        var processes = Process.GetProcesses();
        var samples = new List<ProcSample>();

        foreach (var process in processes)
        {
            try
            {
                var sample = new ProcSample
                {
                    Name = process.ProcessName,
                    Pid = process.Id,
                    Path = GetProcessPath(process),
                    User = GetProcessOwner(process.Id)
                };

                // Get initial CPU time
                var startTime = process.TotalProcessorTime;
                
                await Task.Delay(3000); // Wait 3 seconds for CPU usage sample
                
                process.Refresh();
                var endTime = process.TotalProcessorTime;
                var cpuUsed = (endTime - startTime).TotalMilliseconds;
                sample.CpuPct = cpuUsed / (3000 * Environment.ProcessorCount) * 100;

                samples.Add(sample);
            }
            catch (Exception)
            {
                // Skip processes we can't access
                continue;
            }
        }

        return samples
            .OrderByDescending(p => p.CpuPct)
            .Take(topN)
            .ToList();
    }

    private string? GetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private string? GetProcessOwner(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_Process WHERE ProcessId = {processId}");
            
            using var results = searcher.Get();
            
            foreach (var process in results.Cast<ManagementObject>())
            {
                var args = new string[] { string.Empty };
                process.InvokeMethod("GetOwner", args);
                return args[0];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting process owner for PID {ProcessId}", processId);
        }

        return null;
    }
}