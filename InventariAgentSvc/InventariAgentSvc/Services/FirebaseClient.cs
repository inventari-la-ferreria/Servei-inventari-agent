using System.Management;
using Google.Cloud.Firestore;
using InventariAgentSvc.Models;
using InventariAgentSvc.Config;
using System.Collections.Generic;
using System.Linq;

namespace InventariAgentSvc.Services;

public class FirebaseClient
{
    private readonly ILogger<FirebaseClient> _logger;
    private readonly FirestoreDb _db;
    private readonly string _projectId;
    private readonly AgentConfig _config;

    public async Task<IReadOnlyList<DocumentSnapshot>> GetDevicesAsync()
    {
        try
        {
            var snapshot = await _db.Collection("pcs").GetSnapshotAsync();
            _logger.LogInformation($"Recuperados {snapshot.Documents.Count} dispositivos de Firestore");
            return snapshot.Documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recuperando dispositivos de Firestore");
            throw;
        }
    }

    public FirebaseClient(ILogger<FirebaseClient> logger, ConfigStore configStore)
    {
        _logger = logger;
        _projectId = "laferreria-inventari";
        _config = configStore.Config;

        string credPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "InventariAgent",
            "firebase-credentials.json"
        );

        if (!File.Exists(credPath))
        {
            throw new FileNotFoundException(
                "Archivo de credenciales de Firebase no encontrado. " +
                "Por favor, coloca el archivo firebase-credentials.json en " +
                "C:\\ProgramData\\InventariAgent\\firebase-credentials.json"
            );
        }

        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credPath);
        _db = FirestoreDb.Create(_projectId);
    }

    public async Task UpdateDeviceHeartbeatAsync(string deviceId, MetricsSnapshot metrics)
    {
        try
        {
            var deviceRef = _db.Collection("pcs").Document(deviceId);
            
            var update = new Dictionary<string, object>
            {
                ["lastHeartbeat"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["metrics"] = new Dictionary<string, object>
                {
                    ["cpuTemp"] = metrics.CpuTempC,
                    ["gpuTemp"] = metrics.GpuTempC,
                    ["cpuUsage"] = metrics.CpuUsagePct,
                    ["ramUsage"] = metrics.RamUsagePct,
                    ["diskFree"] = metrics.DiskFreePct
                }
            };

            await deviceRef.SetAsync(update, SetOptions.MergeAll);
            
            _logger.LogInformation(
                "Heartbeat actualizado para {DeviceId} | CPU: {CpuUsage}% | Temp: {CpuTemp}°C",
                deviceId,
                metrics.CpuUsagePct.ToString("F1"),
                metrics.CpuTempC.ToString("F1")
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando heartbeat para {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task RegisterDeviceAsync(string deviceId)
    {
        try
        {
            var deviceRef = _db.Collection("pcs").Document(deviceId);

            // Gather basic specs via WMI (Windows)
            string cpu = "Unknown";
            string gpu = "Unknown";
            double ramGb = 0;
            long storageGb = 0;
            string ipAddress = "Unknown";
            string macAddress = "Unknown";

            try
            {
                using var mos = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (ManagementObject mo in mos.Get())
                {
                    cpu = mo["Name"]?.ToString() ?? cpu;
                    break;
                }

                using var vmos = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                foreach (ManagementObject mo in vmos.Get())
                {
                    gpu = mo["Name"]?.ToString() ?? gpu;
                    break;
                }

                using var cs = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject mo in cs.Get())
                {
                    if (long.TryParse(mo["TotalPhysicalMemory"]?.ToString(), out var mem))
                    {
                        ramGb = Math.Round(mem / (1024.0 * 1024.0 * 1024.0));
                    }
                    break;
                }

                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    storageGb += drive.TotalSize / (1024 * 1024 * 1024);
                }

                // Obtener IP y MAC
                using var netAdapters = new ManagementObjectSearcher("SELECT IPAddress, MACAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");
                foreach (ManagementObject mo in netAdapters.Get())
                {
                    var ips = mo["IPAddress"] as string[];
                    var mac = mo["MACAddress"]?.ToString();
                    
                    if (ips != null && ips.Length > 0 && !string.IsNullOrEmpty(mac))
                    {
                        ipAddress = ips[0]; // Primera IP (IPv4 generalmente)
                        macAddress = mac;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No fue posible obtener todas las especificaciones locales (WMI)");
            }

            // Solo actualizar specs técnicas, preservar campos de configuración (location, tag, brand_model, os)
            var updates = new Dictionary<string, object>
            {
                ["cpu"] = cpu,
                ["ram_gb"] = ramGb,
                ["ip_address"] = ipAddress,
                ["mac_address"] = macAddress,
                ["updatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["specs"] = new Dictionary<string, object>
                {
                    ["cpu"] = cpu,
                    ["gpu"] = gpu,
                    ["ram"] = ramGb.ToString(),
                    ["storage"] = storageGb.ToString(),
                    ["ip"] = ipAddress,
                    ["mac"] = macAddress
                }
            };

            // Solo actualizar si el documento existe (no crear nuevos)
            var docSnapshot = await deviceRef.GetSnapshotAsync();
            if (!docSnapshot.Exists)
            {
                _logger.LogWarning("El PC {DeviceId} no existe en Firestore. No se puede actualizar.", deviceId);
                return;
            }

            await deviceRef.SetAsync(updates, SetOptions.MergeAll);

            _logger.LogInformation("Especificaciones actualizadas en Firestore: {DeviceId}", deviceId);
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando dispositivo {DeviceId}", deviceId);
        }
    }

    public async Task<DocumentReference?> GetRecentOpenIncidentAsync(string deviceId, string metricTag, string severity)
    {
        try
        {
            var fifteenMinutesAgo = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-15));
            var query = _db.Collection("pcs").Document(deviceId).Collection("incidents")
                .WhereEqualTo("status", "open")
                .WhereEqualTo("priority", severity)
                .WhereArrayContains("tags", metricTag)
                .WhereGreaterThan("createdAt", fifteenMinutesAgo)
                .OrderByDescending("createdAt")
                .Limit(1);

            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.FirstOrDefault()?.Reference;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando incidencias recientes para {DeviceId}", deviceId);
            return null;
        }
    }

    public async Task<DocumentSnapshot?> GetOpenIncidentByTagAsync(string deviceId, string metricTag)
    {
        try
        {
            var query = _db.Collection("pcs").Document(deviceId).Collection("incidents")
                .WhereEqualTo("status", "open")
                .WhereArrayContains("tags", metricTag)
                .OrderByDescending("createdAt")
                .Limit(1);

            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error buscando incidencia abierta por tag para {DeviceId}", deviceId);
            return null;
        }
    }

    public async Task AppendChangeAsync(DocumentReference docRef, string metric, double value, double threshold, string note)
    {
        try
        {
            var change = new Dictionary<string, object>
            {
                ["at"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["metric"] = metric,
                ["value"] = value,
                ["threshold"] = threshold,
                ["note"] = note
            };

            var updates = new Dictionary<string, object>
            {
                ["changes"] = FieldValue.ArrayUnion(change),
                ["updatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow)
            };

            await docRef.UpdateAsync(updates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error añadiendo cambio a la incidencia {IncidentId}", docRef.Id);
        }
    }

    public async Task OpenIncidentAsync(
        string deviceId,
        string category,
        string description,
        string priority,
        List<string> tags)
    {
        try
        {
            var incident = new Dictionary<string, object>
            {
                ["title"] = description,
                ["description"] = "",
                ["category"] = category,
                ["priority"] = priority,
                ["status"] = "open",
                ["tags"] = tags,
                ["reportedByName"] = "InventariAgent",
                ["reportedByEmail"] = "InventariAgent@admin.com",
                ["pcId"] = deviceId,
                ["createdBy"] = "system",
                ["createdAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["updatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["attachments"] = new List<object>(),
                ["changes"] = new List<object>(),
                ["comments"] = new List<object>(),
                ["meToo"] = new List<object>()
            };

            var docRef = await _db.Collection("pcs").Document(deviceId).Collection("incidents").AddAsync(incident);
            
            _logger.LogWarning(
                "Nueva incidencia creada: {DocId}\nDispositivo: {DeviceId}\nTipo: {Category}\nMensaje: {Description}",
                docRef.Id,
                deviceId,
                category,
                description
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando incidencia para {DeviceId}", deviceId);
            throw;
        }
    }

    /// <summary>
    /// Obtiene el comando de actualización pendiente para un dispositivo
    /// </summary>
    public async Task<Dictionary<string, object>?> GetUpdateCommandAsync(string deviceId)
    {
        try
        {
            var deviceRef = _db.Collection("pcs").Document(deviceId);
            var snapshot = await deviceRef.GetSnapshotAsync();

            if (snapshot.Exists && snapshot.ContainsField("updateCommand"))
            {
                var updateCommand = snapshot.GetValue<Dictionary<string, object>>("updateCommand");
                return updateCommand;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo comando de actualización para {DeviceId}", deviceId);
            return null;
        }
    }

    /// <summary>
    /// Limpia el comando de actualización después de procesarlo
    /// </summary>
    public async Task ClearUpdateCommandAsync(string deviceId)
    {
        try
        {
            var deviceRef = _db.Collection("pcs").Document(deviceId);
            await deviceRef.UpdateAsync("updateCommand", FieldValue.Delete);
            _logger.LogInformation("Comando de actualización eliminado para {DeviceId}", deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error limpiando comando de actualización para {DeviceId}", deviceId);
        }
    }

    /// <summary>
    /// Actualiza el estado de la actualización en progreso
    /// </summary>
    public async Task SetUpdateStatusAsync(string deviceId, string status, string message)
    {
        try
        {
            var deviceRef = _db.Collection("pcs").Document(deviceId);
            var update = new Dictionary<string, object>
            {
                ["updateStatus"] = new Dictionary<string, object>
                {
                    ["status"] = status,
                    ["message"] = message,
                    ["timestamp"] = Timestamp.FromDateTime(DateTime.UtcNow)
                }
            };

            await deviceRef.SetAsync(update, SetOptions.MergeAll);
            _logger.LogInformation("Estado de actualización establecido: {Status} - {Message}", status, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estableciendo estado de actualización para {DeviceId}", deviceId);
        }
    }
}