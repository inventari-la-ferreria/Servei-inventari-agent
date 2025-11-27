using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.Tasks;
using InventariAgentSvc.Config;
using Microsoft.Extensions.Logging;

namespace InventariAgentSvc.Services;

public sealed record BlockEntry(string name, string category);
public sealed record AppBlockPolicy(List<BlockEntry> blocked, List<BlockEntry> explicitly_allowed, List<string> allowed_tools_clase);

[SupportedOSPlatform("windows")]
public sealed class AppBlocker : IDisposable
{
    private readonly ILogger<AppBlocker> _logger;
    private readonly ManagementEventWatcher _startWatcher;
    
    private Dictionary<string, string> _blocked = new();
    private HashSet<string> _explicit = new();
    private HashSet<string> _tools = new();

    public FirebaseClient? Fb { get; set; }
    public string DeviceId { get; set; } = "";
    public ConfigStore Config { get; set; }

    public AppBlocker(ILogger<AppBlocker> logger, ConfigStore configStore)
    {
        _logger = logger;
        Config = configStore;

        var q = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
        _startWatcher = new ManagementEventWatcher(q);
        _startWatcher.EventArrived += OnProcessStarted;
    }

    public void LoadPolicy(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var policy = JsonSerializer.Deserialize<AppBlockPolicy>(json)!;

            _blocked = policy.blocked
                .ToDictionary(e => Path.GetFileName(e.name).ToLowerInvariant(), e => e.category);
            _explicit = policy.explicitly_allowed
                .Select(e => Path.GetFileName(e.name).ToLowerInvariant())
                .ToHashSet();
            _tools = policy.allowed_tools_clase
                .Select(n => Path.GetFileName(n).ToLowerInvariant())
                .ToHashSet();
            
            _logger.LogInformation("Política de bloqueo de aplicaciones cargada. {BlockedCount} aplicaciones bloqueadas.", _blocked.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando la política de bloqueo de aplicaciones desde {Path}", path);
        }
    }

    public void Start()
    {
        _logger.LogInformation("Iniciando el monitor de bloqueo de aplicaciones.");
        // Barrido inicial
        foreach (var p in Process.GetProcesses())
        {
            _ = TryBlockAsync(p.ProcessName + ".exe", p.Id);
        }

        _startWatcher.Start();
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        var exe = (string)e.NewEvent["ProcessName"];
        var pid = (uint)e.NewEvent["ProcessID"];
        _ = TryBlockAsync(exe, (int)pid);
    }

    private async Task TryBlockAsync(string exeName, int pid)
    {
        var exeLower = exeName.ToLowerInvariant();

        // Detectar TLauncher corriendo bajo Java
        if (exeLower == "java.exe" || exeLower == "javaw.exe")
        {
            if (IsTLauncherProcess(pid))
            {
                _logger.LogInformation("Detectado proceso Java ejecutando TLauncher (pid {Pid})", pid);
                exeName = "TLauncher.exe";
                exeLower = "tlauncher.exe";
            }
        }

        if (IsAllowed(exeLower))
        {
            _logger.LogDebug("Aplicaci��n permitida (lista blanca): {Exe}", exeName);
            return;
        }
        if (!IsBlocked(exeLower, out var appCategory))
        {
            _logger.LogDebug("Aplicaci��n no configurada en la lista de bloqueo: {Exe}", exeName);
            return;
        }

        _logger.LogInformation("Detectada aplicaci��n bloqueada: {Exe} (categor��a: {Category}) pid {Pid}", exeName, appCategory, pid);
        var ok = await TerminateProcessAsync(pid, exeName);

        if (Fb == null)
        {
            _logger.LogWarning("FirebaseClient no configurado; no se reportar�� incidente para {Exe}", exeName);
            return;
        }

        await ReportPolicyIncidentAsync(exeName, pid, appCategory, ok);
    }

    private bool IsAllowed(string exeNameLower)
    {
        if (_explicit.Contains(exeNameLower)) return true;
        if (_tools.Contains(exeNameLower)) return true;
        return false;
    }

    private bool IsBlocked(string exeNameLower, out string category)
    {
        return _blocked.TryGetValue(exeNameLower, out category!);
    }

    private async Task<bool> TerminateProcessAsync(int pid, string exeName)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (p.HasExited) return true;

            _logger.LogInformation("Intentando cerrar proceso bloqueado {Name} (pid {Pid})", p.ProcessName, pid);

            try
            {
                if (p.CloseMainWindow())
                {
                    await Task.Delay(2000);
                    p.Refresh();
                    if (p.HasExited) return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "CloseMainWindow falló para pid {Pid}", pid);
            }

            p.Refresh();
            if (!p.HasExited)
            {
                try
                {
                    p.Kill(true);
                    if (p.WaitForExit(3000)) return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Kill falló para pid {Pid}, probando taskkill /F /T", pid);
                }
            }

            // Fallback a taskkill /F /T para matar árbol de procesos
            var taskKilled = await Task.Run(() => TryTaskKill(pid));
            if (taskKilled) return true;

            // Fallback final: taskkill por nombre de imagen (para casos como TLauncher que resisten por PID)
            var nameKilled = await Task.Run(() => TryTaskKillByName(exeName));
            if (nameKilled) return true;

            // Verificar si sigue vivo
            try
            {
                using var check = Process.GetProcessById(pid);
                if (check.HasExited) return true;
            }
            catch
            {
                return true; // ya no existe
            }

            _logger.LogWarning("No se pudo cerrar el proceso pid {Pid}", pid);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error intentando finalizar pid {Pid}", pid);
            return false;
        }
    }

    private bool TryTaskKill(int pid)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/PID {pid} /F /T",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi)!;
            proc.WaitForExit(4000);
            var success = proc.ExitCode == 0;
            if (!success)
            {
                var err = proc.StandardError.ReadToEnd();
                // Si el error es que no existe (128), consideramos éxito
                if (proc.ExitCode == 128) return true;
                
                _logger.LogWarning("taskkill devolvió {ExitCode} para pid {Pid}. Error: {Err}", proc.ExitCode, pid, err);
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "taskkill fallback falló para pid {Pid}", pid);
            return false;
        }
    }

    private bool TryTaskKillByName(string exeName)
    {
        try
        {
            _logger.LogInformation("Intentando matar proceso por nombre: {Exe}", exeName);
            var psi = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/IM \"{exeName}\" /F",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi)!;
            proc.WaitForExit(4000);
            
            // 0 = éxito, 128 = no encontrado (ya muerto)
            var success = proc.ExitCode == 0 || proc.ExitCode == 128;
            
            if (!success)
            {
                var err = proc.StandardError.ReadToEnd();
                _logger.LogWarning("taskkill /IM devolvió {ExitCode} para {Exe}. Error: {Err}", proc.ExitCode, exeName, err);
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "taskkill by name fallback falló para {Exe}", exeName);
            return false;
        }
    }

    private async Task ReportPolicyIncidentAsync(string exe, int pid, string appCategory, bool terminated)
    {
        var severity = (appCategory is "launcher_juegos" or "emulador" or "android_emulador") ? "high" : "medium";
        var desc = terminated
            ? $"Aplicación bloqueada: {exe} ({appCategory}) — proceso {pid} finalizado por política"
            : $"Intento de bloqueo fallido: {exe} ({appCategory}) — proceso {pid} no pudo cerrarse";

        _logger.LogInformation("Buscando incidencia reciente de tipo 'policy' y severidad '{Severity}'", severity);
        var existing = await Fb.GetRecentOpenIncidentAsync(DeviceId, "policy", severity);

        if (existing is null)
        {
            _logger.LogInformation("No se encontró incidencia reciente. Creando una nueva.");
            await Fb.OpenIncidentAsync(
                DeviceId,
                "policy",
                desc,
                severity,
                new List<string> { "auto","alert","policy","appblock" }
            );
        }
        else
        {
            _logger.LogInformation("Se encontró una incidencia reciente. Añadiendo cambio a la incidencia {IncidentId}", existing.Id);
            await Fb.AppendChangeAsync(existing, "policy.appblock", 0, 0, $"{exe} ({appCategory}) pid={pid} {(terminated ? "terminado" : "fallo")}");
        }
    }

    public void Dispose()
    {
        _startWatcher.Stop();
        _startWatcher.Dispose();
    }

    private bool IsTLauncherProcess(int pid)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            using var objects = searcher.Get();
            foreach (ManagementObject obj in objects)
            {
                var cmdLine = obj["CommandLine"]?.ToString();
                if (!string.IsNullOrEmpty(cmdLine) && 
                    cmdLine.Contains("TLauncher", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error verificando línea de comandos para pid {Pid}", pid);
        }
        return false;
    }
}
