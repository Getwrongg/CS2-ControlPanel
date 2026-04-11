namespace CS2AdminTool.Models;

public class PlayerSnapshot
{
    public string UserId { get; set; } = string.Empty;
    public string Slot { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SteamId { get; set; } = string.Empty;
    public string SteamId64 { get; set; } = string.Empty;
    public string Steam3 { get; set; } = string.Empty;
    public string Connected { get; set; } = string.Empty;
    public string Ping { get; set; } = string.Empty;
    public string Loss { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Rate { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    public string BestIdentifier => !string.IsNullOrWhiteSpace(SteamId64)
        ? SteamId64
        : !string.IsNullOrWhiteSpace(SteamId)
            ? SteamId
            : !string.IsNullOrWhiteSpace(Steam3)
                ? Steam3
                : UserId;
}
