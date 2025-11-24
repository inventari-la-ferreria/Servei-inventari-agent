using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace InventariAgentSvc.Services;

public class RemoteUpdateService
{
    private readonly ILogger<RemoteUpdateService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _installPath;
    private readonly string _updateScriptPath;

    public RemoteUpdateService(ILogger<RemoteUpdateService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(10); // Timeout largo para descargas grandes
        
        _installPath = AppDomain.CurrentDomain.BaseDirectory;
        _updateScriptPath = Path.Combine(_installPath, "auto-update.ps1");
    }

    /// <summary>
    /// Descarga el paquete de actualización desde una URL pública
    /// </summary>
    public async Task<string> DownloadUpdateAsync(string downloadUrl, string version)
    {
        try
        {
            _logger.LogInformation("Descargando actualización desde: {Url}", downloadUrl);

            var tempDir = Path.Combine(Path.GetTempPath(), "InventariAgent_Update");
            Directory.CreateDirectory(tempDir);

            var zipPath = Path.Combine(tempDir, $"update_{version}.zip");

            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fileStream);
                }
            }

            _logger.LogInformation("Actualización descargada exitosamente: {ZipPath}", zipPath);
            return zipPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error descargando la actualización desde {Url}", downloadUrl);
            throw;
        }
    }

    /// <summary>
    /// Extrae el ZIP de actualización
    /// </summary>
    public async Task<string> ExtractUpdateAsync(string zipPath)
    {
        try
        {
            var extractDir = Path.Combine(Path.GetTempPath(), "InventariAgent_Extracted", Guid.NewGuid().ToString());
            Directory.CreateDirectory(extractDir);

            _logger.LogInformation("Extrayendo actualización a: {ExtractDir}", extractDir);

            await Task.Run(() => System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir));

            _logger.LogInformation("Actualización extraída exitosamente");
            return extractDir;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extrayendo la actualización");
            throw;
        }
    }

    /// <summary>
    /// Aplica la actualización ejecutando un script PowerShell que:
    /// 1. Espera a que el servicio termine
    /// 2. Copia los nuevos archivos
    /// 3. Reinicia el servicio
    /// </summary>
    public void ApplyUpdate(string extractedPath)
    {
        try
        {
            _logger.LogInformation("Preparando script de actualización automática...");

            // Crear script PowerShell de actualización
            var scriptContent = $@"
# Auto-Update Script - Generado automáticamente
param(
    [string]$SourcePath = '{extractedPath}',
    [string]$InstallPath = '{_installPath}',
    [string]$ServiceName = 'InventariAgent'
)

Start-Sleep -Seconds 5  # Esperar a que el servicio termine

Write-Host ""Aplicando actualización...""

# Detener el servicio si aún está corriendo
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -eq 'Running') {{
    Stop-Service -Name $ServiceName -Force
    Start-Sleep -Seconds 3
}}

# Respaldar ejecutable actual
$exePath = Join-Path $InstallPath 'InventariAgentSvc.exe'
$backupPath = Join-Path $InstallPath 'InventariAgentSvc.exe.backup'
if (Test-Path $exePath) {{
    Copy-Item -Path $exePath -Destination $backupPath -Force
}}

# Copiar nuevos archivos
try {{
    Copy-Item -Path ""$SourcePath\*"" -Destination $InstallPath -Recurse -Force -ErrorAction Stop
    Write-Host ""Archivos actualizados correctamente.""
    
    # Eliminar backup si la copia fue exitosa
    if (Test-Path $backupPath) {{
        Remove-Item -Path $backupPath -Force
    }}
}} catch {{
    Write-Error ""Error copiando archivos: $_""
    # Restaurar backup si falla
    if (Test-Path $backupPath) {{
        Copy-Item -Path $backupPath -Destination $exePath -Force
    }}
    exit 1
}}

# Iniciar el servicio
try {{
    Start-Service -Name $ServiceName -ErrorAction Stop
    Write-Host ""Servicio reiniciado exitosamente.""
}} catch {{
    Write-Error ""Error iniciando el servicio: $_""
}}

# Limpiar archivos temporales
Start-Sleep -Seconds 2
Remove-Item -Path $SourcePath -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""Actualización completada.""
";

            File.WriteAllText(_updateScriptPath, scriptContent);
            _logger.LogInformation("Script de actualización creado en: {ScriptPath}", _updateScriptPath);

            // Ejecutar el script en un proceso separado
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{_updateScriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(psi);
            _logger.LogInformation("Script de actualización iniciado. El servicio se reiniciará automáticamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aplicando la actualización");
            throw;
        }
    }

    /// <summary>
    /// Proceso completo de actualización
    /// </summary>
    public async Task<bool> PerformUpdateAsync(string downloadUrl, string version)
    {
        try
        {
            _logger.LogWarning("=== INICIANDO ACTUALIZACIÓN REMOTA ===");
            _logger.LogWarning("Versión: {Version}", version);
            _logger.LogWarning("URL: {Url}", downloadUrl);

            // 1. Descargar
            var zipPath = await DownloadUpdateAsync(downloadUrl, version);

            // 2. Extraer
            var extractedPath = await ExtractUpdateAsync(zipPath);

            // 3. Aplicar actualización (esto detendrá el servicio)
            ApplyUpdate(extractedPath);

            _logger.LogWarning("=== ACTUALIZACIÓN INICIADA - EL SERVICIO SE REINICIARÁ ===");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el proceso de actualización");
            return false;
        }
    }
}
