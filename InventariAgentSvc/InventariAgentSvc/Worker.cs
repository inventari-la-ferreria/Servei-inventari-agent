using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using InventariAgentSvc.Config;
using InventariAgentSvc.Services;
using System.Collections.Generic;
using System.IO;

namespace InventariAgentSvc;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly MetricsCollector _metricsCollector;
    private readonly FirebaseClient _firebaseClient;
    private readonly ConfigStore _configStore;
    private readonly AppBlocker _appBlocker;
    private readonly RemoteUpdateService _updateService;
    private readonly GitHubReleaseChecker _releaseChecker;
    private readonly IncidentMailSender _mailSender;
    private readonly HostsBlocker _hostsBlocker;
    private readonly NotificationService _notificationService;
    private const string SERVICE_VERSION = "1.0.47"; // Actualizar con cada release
    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private const int UPDATE_CHECK_INTERVAL_HOURS = 1; // Verificar cada hora
    private DateTime _lastHeartbeatTime = DateTime.MinValue;
    private bool _lastExamModeState = false;

    public Worker(
        ILogger<Worker> logger,
        MetricsCollector metricsCollector,
        FirebaseClient firebaseClient,
        ConfigStore configStore,
        AppBlocker appBlocker,
        RemoteUpdateService updateService,
        GitHubReleaseChecker releaseChecker,
        IncidentMailSender mailSender,
        HostsBlocker hostsBlocker,
        NotificationService notificationService)
    {
        _logger = logger;
        _metricsCollector = metricsCollector;
        _firebaseClient = firebaseClient;
        _configStore = configStore;
        _appBlocker = appBlocker;
        _updateService = updateService;
        _releaseChecker = releaseChecker;
        _mailSender = mailSender;
        _hostsBlocker = hostsBlocker;
        _notificationService = notificationService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Servicio iniciado para dispositivo: {DeviceId}", _configStore.Config.DeviceId);
            _logger.LogInformation("Versión actual del servicio: {Version}", SERVICE_VERSION);
            
            // Inicializar estado de ExamMode
            _lastExamModeState = _configStore.Config.ExamMode;
            if (_lastExamModeState)
            {
                _hostsBlocker.EnableExamMode();
            }
            else
            {
                // Asegurar que esté desactivado si la config dice false (por si quedó sucio)
                _hostsBlocker.DisableExamMode();
            }
            
            // Limpiar estado de actualización en Firestore al iniciar
            try
            {
                await _firebaseClient.SetUpdateStatusAsync(
                    _configStore.Config.DeviceId,
                    "idle",
                    $"Servicio iniciado: v{SERVICE_VERSION}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo actualizar el estado de versión en Firestore");
            }

            // Verificar si hay una actualización disponible en GitHub Releases
            await CheckForUpdatesAsync();
            
            // Register device metadata in Firestore (one-time at startup)
            try
            {
                await _firebaseClient.RegisterDeviceAsync(_configStore.Config.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo registrar el dispositivo en Firestore al iniciar");
            }

            // Cargar política de bloqueo de apps y arrancar el monitor
            _appBlocker.Fb = _firebaseClient;
            _appBlocker.DeviceId = _configStore.Config.DeviceId;
            var policyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "appblock.json");
            _appBlocker.LoadPolicy(policyPath);
            _appBlocker.Start();

            // Check inicial de ExamMode remoto (al arrancar)
            await CheckRemoteExamModeAsync();

            // Verificar cambios de hardware (Robo)
            await CheckHardwareChangesAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Verificar actualizaciones periódicamente (cada hora)
                    if (DateTime.UtcNow - _lastUpdateCheck > TimeSpan.FromHours(UPDATE_CHECK_INTERVAL_HOURS))
                    {
                        await CheckForUpdatesAsync();
                        // Aprovechamos el ciclo de 1 hora para revisar también la config remota (ExamMode)
                        await CheckRemoteExamModeAsync();
                        _lastUpdateCheck = DateTime.UtcNow;
                    }

                    var metrics = await _metricsCollector.CaptureAsync();
                    var thresholds = _configStore.Config.Thresholds;

                    // Lógica de Heartbeat optimizada:
                    // 1. Si se supera algún umbral de advertencia -> Actualizar YA
                    // 2. Si ha pasado 1 hora desde el último heartbeat -> Actualizar (Keep-alive)
                    
                    bool isCritical = 
                        metrics.CpuTempC >= thresholds.CpuTempWarn ||
                        metrics.GpuTempC >= thresholds.GpuTempWarn ||
                        metrics.RamUsagePct >= 80 || // Hardcoded warn threshold for RAM if not in config
                        metrics.DiskFreePct < 25;    // Hardcoded warn threshold for Disk if not in config

                    bool isKeepAliveDue = DateTime.UtcNow - _lastHeartbeatTime >= TimeSpan.FromHours(1);

                    if (isCritical || isKeepAliveDue)
                    {
                        if (isCritical) _logger.LogInformation("⚠️ Métricas fuera de rango, forzando actualización de heartbeat.");
                        else _logger.LogInformation("⏰ Hora de heartbeat programado (Keep-alive).");

                        await _firebaseClient.UpdateDeviceHeartbeatAsync(
                            _configStore.Config.DeviceId,
                            metrics
                        );
                        _lastHeartbeatTime = DateTime.UtcNow;
                    }


                    // Check for critical metrics and create incidents
                    var deviceId = _configStore.Config.DeviceId;
                    var policy = _configStore.Config.IncidentPolicy ?? new Config.IncidentPolicy();

                    // CPU Temperature
                    var cpuTempTags = new List<string> { "auto", "alert", "cpu", "temperature" };
                    if (metrics.CpuTempC >= _configStore.Config.Thresholds.CpuTempCrit)
                    {
                        var metricTag = "cpu_temp_crit";
                        var open = await _firebaseClient.GetOpenIncidentByTagAsync(deviceId, metricTag);
                        if (open != null)
                        {
                            var last = (open.TryGetValue("updatedAt", out Google.Cloud.Firestore.Timestamp tsUpd)
                                ? tsUpd.ToDateTime()
                                : open.GetValue<Google.Cloud.Firestore.Timestamp>("createdAt").ToDateTime());
                            if (DateTime.UtcNow - last >= TimeSpan.FromMinutes(policy.RepeatUpdateCooldownMinutes))
                            {
                                await _firebaseClient.AppendChangeAsync(open.Reference, "cpuTemp", metrics.CpuTempC, _configStore.Config.Thresholds.CpuTempCrit, "Persistente: temperatura CPU crítica");
                            }
                        }
                        else
                        {
                            var tags = new List<string>(cpuTempTags) { metricTag };
                            var desc = $"Temperatura CPU crítica: {metrics.CpuTempC:F1}°C (límite: {_configStore.Config.Thresholds.CpuTempCrit}°C)";
                            await _firebaseClient.OpenIncidentAsync(
                                deviceId,
                                "performance",
                                desc,
                                "high",
                                tags
                            );
                            // Enviar correo (no bloqueante)
                            _ = _mailSender.SendIncidentMailAsync(deviceId, "performance", desc, "high");
                        }
                    }
                    else if (metrics.CpuTempC >= _configStore.Config.Thresholds.CpuTempWarn)
                    {
                        var metricTag = "cpu_temp_warn";
                        var open = await _firebaseClient.GetOpenIncidentByTagAsync(deviceId, metricTag);
                        if (open != null)
                        {
                            var last = (open.TryGetValue("updatedAt", out Google.Cloud.Firestore.Timestamp tsUpd)
                                ? tsUpd.ToDateTime()
                                : open.GetValue<Google.Cloud.Firestore.Timestamp>("createdAt").ToDateTime());
                            if (DateTime.UtcNow - last >= TimeSpan.FromMinutes(policy.RepeatUpdateCooldownMinutes))
                            {
                                await _firebaseClient.AppendChangeAsync(open.Reference, "cpuTemp", metrics.CpuTempC, _configStore.Config.Thresholds.CpuTempWarn, "Persistente: temperatura CPU alta");
                            }
                        }
                        else
                        {
                            var tags = new List<string>(cpuTempTags) { metricTag };
                            var desc = $"Temperatura CPU alta: {metrics.CpuTempC:F1}°C (límite: {_configStore.Config.Thresholds.CpuTempWarn}°C)";
                            await _firebaseClient.OpenIncidentAsync(
                                deviceId,
                                "performance",
                                desc,
                                "medium",
                                tags
                            );
                            _ = _mailSender.SendIncidentMailAsync(deviceId, "performance", desc, "medium");
                        }
                    }

                    // GPU Temperature
                    var gpuTempTags = new List<string> { "auto", "alert", "gpu", "temperature" };
                    if (metrics.GpuTempC >= _configStore.Config.Thresholds.GpuTempCrit)
                    {
                        var metricTag = "gpu_temp_crit";
                        var open = await _firebaseClient.GetOpenIncidentByTagAsync(deviceId, metricTag);
                        if (open != null)
                        {
                            var last = (open.TryGetValue("updatedAt", out Google.Cloud.Firestore.Timestamp tsUpd)
                                ? tsUpd.ToDateTime()
                                : open.GetValue<Google.Cloud.Firestore.Timestamp>("createdAt").ToDateTime());
                            if (DateTime.UtcNow - last >= TimeSpan.FromMinutes(policy.RepeatUpdateCooldownMinutes))
                            {
                                await _firebaseClient.AppendChangeAsync(open.Reference, "gpuTemp", metrics.GpuTempC, _configStore.Config.Thresholds.GpuTempCrit, "Persistente: temperatura GPU crítica");
                            }
                        }
                        else
                        {
                            var tags = new List<string>(gpuTempTags) { metricTag };
                            var desc = $"Temperatura GPU crítica: {metrics.GpuTempC:F1}°C (límite: {_configStore.Config.Thresholds.GpuTempCrit}°C)";
                            await _firebaseClient.OpenIncidentAsync(
                                deviceId,
                                "performance",
                                desc,
                                "high",
                                tags
                            );
                            _ = _mailSender.SendIncidentMailAsync(deviceId, "performance", desc, "high");
                        }
                    }
                    else if (metrics.GpuTempC >= _configStore.Config.Thresholds.GpuTempWarn)
                    {
                        var metricTag = "gpu_temp_warn";
                        var open = await _firebaseClient.GetOpenIncidentByTagAsync(deviceId, metricTag);
                        if (open != null)
                        {
                            var last = (open.TryGetValue("updatedAt", out Google.Cloud.Firestore.Timestamp tsUpd)
                                ? tsUpd.ToDateTime()
                                : open.GetValue<Google.Cloud.Firestore.Timestamp>("createdAt").ToDateTime());
                            if (DateTime.UtcNow - last >= TimeSpan.FromMinutes(policy.RepeatUpdateCooldownMinutes))
                            {
                                await _firebaseClient.AppendChangeAsync(open.Reference, "gpuTemp", metrics.GpuTempC, _configStore.Config.Thresholds.GpuTempWarn, "Persistente: temperatura GPU alta");
                            }
                        }
                        else
                        {
                            var tags = new List<string>(gpuTempTags) { metricTag };
                            var desc = $"Temperatura GPU alta: {metrics.GpuTempC:F1}°C (límite: {_configStore.Config.Thresholds.GpuTempWarn}°C)";
                            await _firebaseClient.OpenIncidentAsync(
                                deviceId,
                                "performance",
                                desc,
                                "medium",
                                tags
                            );
                            _ = _mailSender.SendIncidentMailAsync(deviceId, "performance", desc, "medium");
                        }
                    }



                    // RAM Usage
                    var ramUsageTags = new List<string> { "auto", "alert", "ram", "usage" };
                    if (metrics.RamUsagePct >= 80)
                    {
                        var metricTag = "ram_usage_crit";
                        var open = await _firebaseClient.GetOpenIncidentByTagAsync(deviceId, metricTag);
                        if (open != null)
                        {
                            var last = (open.TryGetValue("updatedAt", out Google.Cloud.Firestore.Timestamp tsUpd)
                                ? tsUpd.ToDateTime()
                                : open.GetValue<Google.Cloud.Firestore.Timestamp>("createdAt").ToDateTime());
                            if (DateTime.UtcNow - last >= TimeSpan.FromMinutes(policy.RepeatUpdateCooldownMinutes))
                            {
                                await _firebaseClient.AppendChangeAsync(open.Reference, "ramUsage", metrics.RamUsagePct, 80, "Persistente: uso de RAM crítico");
                            }
                        }
                        else
                        {
                            var tags = new List<string>(ramUsageTags) { metricTag };
                            var desc = $"Uso de RAM crítico: {metrics.RamUsagePct:F1}% (límite: 80%)";
                            await _firebaseClient.OpenIncidentAsync(
                                deviceId,
                                "memory",
                                desc,
                                "high",
                                tags
                            );
                            _ = _mailSender.SendIncidentMailAsync(deviceId, "memory", desc, "high");
                        }
                    }

                    // Disk Space
                    var diskSpaceTags = new List<string> { "auto", "alert", "disk", "space" };
                    if (metrics.DiskFreePct < 25)
                    {
                        var metricTag = "disk_space_warn";
                        var open = await _firebaseClient.GetOpenIncidentByTagAsync(deviceId, metricTag);
                        if (open != null)
                        {
                            var last = (open.TryGetValue("updatedAt", out Google.Cloud.Firestore.Timestamp tsUpd)
                                ? tsUpd.ToDateTime()
                                : open.GetValue<Google.Cloud.Firestore.Timestamp>("createdAt").ToDateTime());
                            if (DateTime.UtcNow - last >= TimeSpan.FromMinutes(policy.RepeatUpdateCooldownMinutes))
                            {
                                await _firebaseClient.AppendChangeAsync(open.Reference, "diskFree", metrics.DiskFreePct, 25, "Persistente: poco espacio en disco");
                            }
                        }
                        else
                        {
                            var tags = new List<string>(diskSpaceTags) { metricTag };
                            var desc = $"Poco espacio en disco: {metrics.DiskFreePct:F1}% libre (límite: 25%)";
                            await _firebaseClient.OpenIncidentAsync(
                                deviceId,
                                "storage",
                                desc,
                                "medium",
                                tags
                            );
                            _ = _mailSender.SendIncidentMailAsync(deviceId, "storage", desc, "medium");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error durante el ciclo de monitoreo");
                }

                await Task.Delay(30000, stoppingToken); // 30 segundos
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Servicio detenido");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Error fatal en el servicio");
            throw;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var release = await _releaseChecker.CheckForNewVersionAsync(SERVICE_VERSION);
            
            if (release != null)
            {
                _logger.LogWarning("=== ACTUALIZACIÓN DISPONIBLE ===");
                _logger.LogWarning("Versión actual: {CurrentVersion}", SERVICE_VERSION);
                _logger.LogWarning("Nueva versión: {NewVersion}", release.Version);
                _logger.LogWarning("Publicada: {PublishedAt}", release.PublishedAt);
                _logger.LogWarning("URL de descarga: {DownloadUrl}", release.DownloadUrl);

                // Notificar en Firestore que se detectó actualización
                try
                {
                    await _firebaseClient.SetUpdateStatusAsync(
                        _configStore.Config.DeviceId, 
                        "downloading", 
                        $"Descargando versión {release.Version}"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo notificar estado en Firestore");
                }

                // Ejecutar actualización (esto detendrá el servicio)
                var success = await _updateService.PerformUpdateAsync(release.DownloadUrl, release.Version);

                if (success)
                {
                    // El servicio se detendrá y reiniciará automáticamente
                    Environment.Exit(0);
                }
                else
                {
                    try
                    {
                        await _firebaseClient.SetUpdateStatusAsync(
                            _configStore.Config.DeviceId, 
                            "failed", 
                            "Error durante la actualización"
                        );
                    }
                    catch { /* Ignorar errores de notificación */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando actualizaciones en GitHub");
        }
    }


    private async Task CheckRemoteExamModeAsync()
    {
        try
        {
            var remoteExamMode = await _firebaseClient.GetExamModeAsync(_configStore.Config.DeviceId);
            
            // Si el estado remoto es diferente al local, actualizamos
            if (remoteExamMode != _configStore.Config.ExamMode)
            {
                _logger.LogInformation("Sincronizando Modo Examen desde remoto: {State}", remoteExamMode);
                _configStore.Config.ExamMode = remoteExamMode;
                await _configStore.SaveAsync();

                // Aplicar cambios
                if (remoteExamMode)
                {
                    _hostsBlocker.EnableExamMode();
                    _notificationService.SendNotification("MODO EXAMEN ACTIVADO: El acceso a herramientas de IA ha sido bloqueado.");
                }
                else
                {
                    _hostsBlocker.DisableExamMode();
                    _notificationService.SendNotification("MODO EXAMEN DESACTIVADO: El acceso a herramientas de IA ha sido restaurado.");
                }
            }
            // Asegurar consistencia (por si se modificó el hosts manualmente)
            else if (_configStore.Config.ExamMode)
            {
                 _hostsBlocker.EnableExamMode();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sincronizando Modo Examen");
        }
    }

    private async Task CheckHardwareChangesAsync()
    {
        try
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return;
            }

            string currentCpu = "";
            long currentRam = 0;

            // Obtener CPU
            using (var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
            {
                foreach (var obj in searcher.Get())
                {
                    currentCpu = obj["Name"]?.ToString() ?? "Unknown";
                    break;
                }
            }

            // Obtener RAM
            using (var searcher = new System.Management.ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory"))
            {
                foreach (var obj in searcher.Get())
                {
                    if (long.TryParse(obj["Capacity"]?.ToString(), out long capacity))
                    {
                        currentRam += capacity;
                    }
                }
            }

            var stored = _configStore.Config.HardwareSpecs;

            // Si es la primera vez, guardamos la configuración base
            if (string.IsNullOrEmpty(stored.CpuName) && stored.TotalRamBytes == 0)
            {
                _logger.LogInformation("Guardando configuración de hardware base: CPU={Cpu}, RAM={Ram} bytes", currentCpu, currentRam);
                _configStore.Config.HardwareSpecs.CpuName = currentCpu;
                _configStore.Config.HardwareSpecs.TotalRamBytes = currentRam;
                await _configStore.SaveAsync();
                return;
            }

            // Verificar cambios (Robo de RAM)
            // Permitimos un pequeño margen de error por si acaso, pero si falta más de 1GB es sospechoso
            if (stored.TotalRamBytes > 0 && currentRam < (stored.TotalRamBytes - 1024 * 1024 * 1024))
            {
                var msg = $"ALERTA DE SEGURIDAD: Se ha detectado una reducción de memoria RAM. Original: {stored.TotalRamBytes / 1024 / 1024} MB, Actual: {currentRam / 1024 / 1024} MB.";
                _logger.LogCritical(msg);
                
                await _firebaseClient.OpenIncidentAsync(
                    _configStore.Config.DeviceId,
                    "security",
                    msg,
                    "critical",
                    new List<string> { "security", "hardware_theft", "ram" }
                );
                _ = _mailSender.SendIncidentMailAsync(_configStore.Config.DeviceId, "security", msg, "critical");
            }

            // Verificar cambio de CPU
            if (!string.IsNullOrEmpty(stored.CpuName) && currentCpu != stored.CpuName)
            {
                var msg = $"ALERTA DE SEGURIDAD: Se ha detectado un cambio de procesador. Original: {stored.CpuName}, Actual: {currentCpu}.";
                _logger.LogCritical(msg);

                await _firebaseClient.OpenIncidentAsync(
                    _configStore.Config.DeviceId,
                    "security",
                    msg,
                    "critical",
                    new List<string> { "security", "hardware_theft", "cpu" }
                );
                _ = _mailSender.SendIncidentMailAsync(_configStore.Config.DeviceId, "security", msg, "critical");
            }

            // Actualizar la config con lo nuevo para no spamear (o podríamos decidir NO actualizar para seguir alertando)
            // En este caso, actualizamos para asumir el nuevo estado tras la alerta
            if (currentRam != stored.TotalRamBytes || currentCpu != stored.CpuName)
            {
                _configStore.Config.HardwareSpecs.CpuName = currentCpu;
                _configStore.Config.HardwareSpecs.TotalRamBytes = currentRam;
                await _configStore.SaveAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando cambios de hardware");
        }
    }
}
