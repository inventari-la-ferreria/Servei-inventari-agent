using System.Text.Json;

namespace InventariAgentSvc.Config;

public class ConfigStore
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "InventariAgent",
        "config.json"
    );

    private readonly ILogger<ConfigStore> _logger;
    private AgentConfig _config;

    public ConfigStore(ILogger<ConfigStore> logger)
    {
        _logger = logger;
        _config = LoadConfig();
    }

    public AgentConfig Config => _config;

    private AgentConfig LoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                _logger.LogInformation("Config file not found, creating default at: {ConfigPath}", ConfigPath);
                return CreateDefaultConfig();
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AgentConfig>(json) ?? CreateDefaultConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading config, using default");
            return CreateDefaultConfig();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(ConfigPath, json);
            _logger.LogInformation("Configuración guardada en: {ConfigPath}", ConfigPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error guardando configuración");
            throw;
        }
    }

    private AgentConfig CreateDefaultConfig()
    {
        var config = new AgentConfig
        {
            DeviceId = "",
            DeviceName = "",
            Thresholds = new Thresholds()
        };

        try
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating default config file");
        }

        return config;
    }
}