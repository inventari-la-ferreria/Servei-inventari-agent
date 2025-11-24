using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace InventariAgentSvc.Services;

public class GitHubReleaseChecker
{
    private readonly ILogger<GitHubReleaseChecker> _logger;
    private readonly HttpClient _httpClient;
    private const string GITHUB_API_URL = "https://api.github.com/repos/inventari-la-ferreria/Servei-inventari-agent/releases/latest";
    
    public GitHubReleaseChecker(ILogger<GitHubReleaseChecker> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "InventariAgent");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public class ReleaseInfo
    {
        public string Version { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ReleaseName { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public DateTime PublishedAt { get; set; }
    }

    /// <summary>
    /// Verifica si hay una nueva versión disponible en GitHub Releases
    /// </summary>
    public async Task<ReleaseInfo?> CheckForNewVersionAsync(string currentVersion)
    {
        try
        {
            _logger.LogInformation("Verificando si hay actualizaciones disponibles...");
            _logger.LogInformation("Versión actual: {CurrentVersion}", currentVersion);

            var response = await _httpClient.GetAsync(GITHUB_API_URL);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("No se pudo obtener información de releases: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<JsonElement>(content);

            // Obtener versión del tag (ej: "v1.0.1" -> "1.0.1")
            var tagName = release.GetProperty("tag_name").GetString() ?? "";
            var latestVersion = tagName.TrimStart('v');

            // Buscar el asset del ZIP
            string? downloadUrl = null;
            if (release.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var assetName = asset.GetProperty("name").GetString() ?? "";
                    if (assetName.EndsWith(".zip") && !assetName.EndsWith(".sha256"))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                _logger.LogWarning("No se encontró archivo ZIP en el release");
                return null;
            }

            _logger.LogInformation("Última versión disponible: {LatestVersion}", latestVersion);

            // Comparar versiones
            if (IsNewerVersion(currentVersion, latestVersion))
            {
                _logger.LogWarning("¡Nueva versión disponible! {CurrentVersion} -> {LatestVersion}", currentVersion, latestVersion);
                
                return new ReleaseInfo
                {
                    Version = latestVersion,
                    DownloadUrl = downloadUrl,
                    ReleaseName = release.GetProperty("name").GetString() ?? "",
                    ReleaseNotes = release.GetProperty("body").GetString() ?? "",
                    PublishedAt = release.GetProperty("published_at").GetDateTime()
                };
            }
            else
            {
                _logger.LogInformation("Ya estás en la última versión");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando actualizaciones en GitHub");
            return null;
        }
    }

    /// <summary>
    /// Compara versiones en formato semántico (ej: 1.0.1 vs 1.0.0)
    /// </summary>
    private bool IsNewerVersion(string current, string latest)
    {
        try
        {
            var currentParts = current.Split('.').Select(int.Parse).ToArray();
            var latestParts = latest.Split('.').Select(int.Parse).ToArray();

            // Comparar major, minor, patch
            for (int i = 0; i < Math.Min(currentParts.Length, latestParts.Length); i++)
            {
                if (latestParts[i] > currentParts[i])
                    return true;
                if (latestParts[i] < currentParts[i])
                    return false;
            }

            // Si todas las partes son iguales, no es más nueva
            return false;
        }
        catch
        {
            // Si falla el parsing, comparar como strings
            return string.Compare(latest, current, StringComparison.Ordinal) > 0;
        }
    }
}
