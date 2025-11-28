using System.Text.Json;

namespace InventariAgentSvc.Config;

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