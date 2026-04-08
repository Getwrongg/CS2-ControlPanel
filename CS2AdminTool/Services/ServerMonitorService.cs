using System.Text.RegularExpressions;
using CS2AdminTool.Models;

namespace CS2AdminTool.Services;

public class ServerMonitorService
{
    private readonly IRconService _rconService;

    public ServerMonitorService(IRconService rconService)
    {
        _rconService = rconService;
    }

    public async Task<(ServerTelemetry telemetry, IReadOnlyList<string> feed)> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var feed = new List<string>();
        feed.Add(Timestamp("[Monitor] Sending: status"));
        var status = await _rconService.SendCommandAsync("status", cancellationToken);
        feed.Add(Timestamp("[Monitor] Received: status"));

        feed.Add(Timestamp("[Monitor] Sending: stats"));
        var stats = await _rconService.SendCommandAsync("stats", cancellationToken);
        feed.Add(Timestamp("[Monitor] Received: stats"));

        var telemetry = Parse(status, stats);
        telemetry.LastRefreshUtc = DateTime.UtcNow;

        return (telemetry, feed);
    }

    private static ServerTelemetry Parse(string status, string stats)
    {
        var telemetry = new ServerTelemetry
        {
            RawStatus = status,
            RawStats = stats,
            CurrentMap = Extract(status, @"map\s*:\s*([^\r\n]+)") ?? "Unknown",
            ServerHostname = Extract(status, @"hostname\s*:\s*([^\r\n]+)") ?? "Unknown"
        };

        var gameType = Extract(status, @"game_type\s*:\s*(\d+)") ?? Extract(stats, @"game_type\s*[:=]\s*(\d+)");
        var gameMode = Extract(status, @"game_mode\s*:\s*(\d+)") ?? Extract(stats, @"game_mode\s*[:=]\s*(\d+)");
        telemetry.GameTypeMode = gameType is null && gameMode is null
            ? "Unknown"
            : $"Type {gameType ?? "?"} / Mode {gameMode ?? "?"}";

        telemetry.ConnectedPlayerCount = ParsePlayerCount(status);
        return telemetry;
    }

    private static int ParsePlayerCount(string status)
    {
        var playersLine = Extract(status, @"players\s*:\s*(\d+)\s*humans");
        if (int.TryParse(playersLine, out var humans))
        {
            return humans;
        }

        var playerRows = status
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.TrimStart().StartsWith('#') && line.Contains('"'));

        return playerRows;
    }

    private static string? Extract(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }

    private static string Timestamp(string msg) => $"[{DateTime.Now:HH:mm:ss}] {msg}";
}
