namespace InventariAgentSvc.Models;

public class MetricsSnapshot
{
    public double CpuTempC { get; set; }
    public double GpuTempC { get; set; }
    public double CpuUsagePct { get; set; }
    public double RamUsagePct { get; set; }
    public double DiskFreePct { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}