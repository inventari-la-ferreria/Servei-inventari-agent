using System.Text.Json;

namespace InventariAgentSvc.Config;

public class AgentConfig
{
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public Thresholds Thresholds { get; set; } = new();
    public IncidentPolicy IncidentPolicy { get; set; } = new();
}

public class Thresholds
{
    public double CpuTempWarn { get; set; } = 85;
    public double CpuTempCrit { get; set; } = 95;
    public double GpuTempWarn { get; set; } = 85;
    public double GpuTempCrit { get; set; } = 95;
    public double CpuUsageWarn { get; set; } = 85;
    public double CpuUsageCrit { get; set; } = 95;
}

public class IncidentPolicy
{
    // Minutos a esperar antes de crear una nueva incidencia del mismo tipo
    public int NewIncidentCooldownMinutes { get; set; } = 120; // 2 horas por defecto

    // Minutos a esperar para volver a anotar (repetir) en una incidencia ya abierta
    public int RepeatUpdateCooldownMinutes { get; set; } = 60; // 1 hora por defecto
}