namespace CS2AdminTool.Models;

public class ServerTelemetry
{
    public string CurrentMap { get; set; } = "Unknown";
    public string GameTypeMode { get; set; } = "Unknown";
    public string ServerHostname { get; set; } = "Unknown";
    public int ConnectedPlayerCount { get; set; }
    public string RawStatus { get; set; } = string.Empty;
    public string RawStats { get; set; } = string.Empty;
    public DateTime? LastRefreshUtc { get; set; }
}
