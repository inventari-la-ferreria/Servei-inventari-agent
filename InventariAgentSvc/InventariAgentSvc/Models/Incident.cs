namespace InventariAgentSvc.Models;

public class Incident
{
    public string Id { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public MetricsSnapshot Metrics { get; set; } = new();
    public List<ProcSample>? Processes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}