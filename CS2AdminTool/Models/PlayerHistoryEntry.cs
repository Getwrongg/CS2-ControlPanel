namespace CS2AdminTool.Models;

public class PlayerHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LastKnownName { get; set; } = string.Empty;
    public string SteamId { get; set; } = string.Empty;
    public string SteamId64 { get; set; } = string.Empty;
    public string Steam3 { get; set; } = string.Empty;
    public string LastKnownIp { get; set; } = string.Empty;
    public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public string LastServerHost { get; set; } = string.Empty;
    public int LastServerPort { get; set; }
    public int TimesSeen { get; set; } = 1;
    public string Notes { get; set; } = string.Empty;
    public bool IsBannedLocally { get; set; }
}
