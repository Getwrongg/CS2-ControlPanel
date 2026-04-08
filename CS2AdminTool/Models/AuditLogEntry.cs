namespace CS2AdminTool.Models;

public class AuditLogEntry
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string CommandText { get; set; } = string.Empty;
    public string ResponsePreview { get; set; } = string.Empty;
}
