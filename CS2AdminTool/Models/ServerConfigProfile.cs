namespace CS2AdminTool.Models;

public class ServerConfigProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Custom";
    public Guid? MapProfileId { get; set; }

    // Snapshot map metadata for portability/history.
    public bool IsWorkshopMap { get; set; }
    public string? WorkshopMapId { get; set; }
    public string? StandardMapName { get; set; }

    public List<CommandEntry> Commands { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunAt { get; set; }
}
