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
            TryBlock(p.ProcessName + ".exe", p.Id);
        }

        _startWatcher.Start();
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        var exe = (string)e.NewEvent["ProcessName"];
        var pid = (uint)e.NewEvent["ProcessID"];
        TryBlock(exe, (int)pid);
    }

    private async void TryBlock(string exeName, int pid)
    {
        var exeLower = exeName.ToLowerInvariant();

        if (IsAllowed(exeLower)) return;
        if (!IsBlocked(exeLower, out var appCategory)) return;

        var ok = await TerminateProcessAsync(pid);
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

    private async Task<bool> TerminateProcessAsync(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (p.HasExited) return true;

            try { if (p.CloseMainWindow()) await Task.Delay(2000); } catch {}

            p.Refresh();
            if (!p.HasExited) p.Kill(true);
            return true;
        }
        catch
        {
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
}