using System.Text.Json;

namespace InventariAgentSvc.Config;

public class BlockedApp
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
}

public class AppBlockConfig
{
    public List<BlockedApp> Blocked { get; set; } = new();
    public List<BlockedApp> ExplicitlyAllowed { get; set; } = new();
    public List<string> AllowedToolsClase { get; set; } = new();
}