namespace InventariAgentSvc.Models;

public class ProcSample
{
    public string Name { get; set; } = "";
    public int Pid { get; set; }
    public double CpuPct { get; set; }
    public string? Path { get; set; }
    public string? User { get; set; }
    public string? Category { get; set; }
}