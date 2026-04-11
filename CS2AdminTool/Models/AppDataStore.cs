namespace CS2AdminTool.Models;

public class AppDataStore
{
    public List<ConfigCategory> Categories { get; set; } = new();
    public List<MapProfile> Maps { get; set; } = new();
    public List<ServerConfigProfile> ServerConfigs { get; set; } = new();
    public List<PlayerHistoryEntry> PlayerHistory { get; set; } = new();
    public RunnerOptions RunnerOptions { get; set; } = new();
}
