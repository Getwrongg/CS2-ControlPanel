namespace CS2AdminTool.Models;

public class PresetCommandPack
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Commands { get; set; } = new();
    public List<string> RollbackCommands { get; set; } = new();
}
