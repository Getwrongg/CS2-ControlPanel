namespace CS2AdminTool.Models;

public class MapProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = "Custom";
    public bool IsWorkshopMap { get; set; }
    public string? WorkshopMapId { get; set; }
    public string? StandardMapName { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}
