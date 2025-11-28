using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace InventariAgentSvc.Services;

[SupportedOSPlatform("windows")]
public class HostsBlocker
{
    private readonly ILogger<HostsBlocker> _logger;
    private const string HostsPath = @"C:\Windows\System32\drivers\etc\hosts";
    private const string BlockIp = "127.0.0.1";
    private const string MarkerStart = "### INVENTARI_AGENT_EXAM_MODE_START ###";
    private const string MarkerEnd = "### INVENTARI_AGENT_EXAM_MODE_END ###";

    private readonly List<string> _aiDomains = new()
    {
        "openai.com", "www.openai.com",
        "chatgpt.com", "www.chatgpt.com",
        "chat.openai.com",
        "gemini.google.com",
        "bard.google.com",
        "claude.ai", "www.claude.ai",
        "copilot.microsoft.com",
        "bing.com", "www.bing.com", // Bing Chat is integrated, hard to block only chat without blocking bing
        "poe.com", "www.poe.com",
        "perplexity.ai", "www.perplexity.ai",
        "you.com", "www.you.com"
    };

    public HostsBlocker(ILogger<HostsBlocker> logger)
    {
        _logger = logger;
    }

    public void EnableExamMode()
    {
        try
        {
            if (!File.Exists(HostsPath))
            {
                _logger.LogError("No se encontró el archivo hosts en {Path}", HostsPath);
                return;
            }

            var lines = File.ReadAllLines(HostsPath).ToList();
            
            // Si ya está activo, no hacemos nada o regeneramos
            if (lines.Any(l => l.Contains(MarkerStart)))
            {
                _logger.LogInformation("El modo examen ya parece estar activo en el archivo hosts.");
                return; // O podríamos limpiar y re-aplicar para asegurar
            }

            var newLines = new List<string> { "", MarkerStart };
            foreach (var domain in _aiDomains)
            {
                newLines.Add($"{BlockIp} {domain}");
            }
            newLines.Add(MarkerEnd);

            File.AppendAllLines(HostsPath, newLines);
            _logger.LogInformation("Modo Examen activado: Se han bloqueado {Count} dominios de IA en el archivo hosts.", _aiDomains.Count);
            
            // Intentar flashear DNS cache
            FlushDns();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al activar el Modo Examen (modificar hosts)");
        }
    }

    public void DisableExamMode()
    {
        try
        {
            if (!File.Exists(HostsPath)) return;

            var lines = File.ReadAllLines(HostsPath).ToList();
            var outputLines = new List<string>();
            var skipping = false;

            foreach (var line in lines)
            {
                if (line.Trim() == MarkerStart)
                {
                    skipping = true;
                    continue;
                }
                if (line.Trim() == MarkerEnd)
                {
                    skipping = false;
                    continue;
                }

                if (!skipping)
                {
                    outputLines.Add(line);
                }
            }

            File.WriteAllLines(HostsPath, outputLines);
            _logger.LogInformation("Modo Examen desactivado: Se han eliminado los bloqueos del archivo hosts.");
            
            FlushDns();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al desactivar el Modo Examen (restaurar hosts)");
        }
    }

    private void FlushDns()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                CreateNoWindow = true,
                UseShellExecute = false
            })?.WaitForExit();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo ejecutar ipconfig /flushdns");
        }
    }
}
