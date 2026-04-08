namespace CS2AdminTool.Models;

public class CommandPreset
{
    public string Name { get; set; } = string.Empty;
    public List<string> Commands { get; set; } = new();
}
