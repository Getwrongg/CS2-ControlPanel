using System.Text.RegularExpressions;
using CS2AdminTool.Models;

namespace CS2AdminTool.Services;

public sealed class PlayerStatusParser
{
    private static readonly Regex TokenRegex = new("\"([^\"]*)\"|(\\S+)", RegexOptions.Compiled);

    public PlayerStatusParseResult Parse(string statusOutput)
    {
        var result = new PlayerStatusParseResult();
        if (string.IsNullOrWhiteSpace(statusOutput))
        {
            return result;
        }

        Dictionary<string, int>? headerColumns = null;

        foreach (var rawLine in statusOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith('#'))
            {
                continue;
            }

            if (line.Contains("userid", StringComparison.OrdinalIgnoreCase) && line.Contains("name", StringComparison.OrdinalIgnoreCase))
            {
                headerColumns = BuildHeaderColumns(Tokenize(line));
                continue;
            }

            var tokens = Tokenize(line);
            if (tokens.Count < 3 || tokens[0] != "#" || !int.TryParse(tokens[1], out _))
            {
                continue;
            }

            result.HadPlayerLines = true;
            var player = ParsePlayer(tokens, headerColumns);
            if (player is not null)
            {
                result.Players.Add(player);
            }
        }

        return result;
    }

    private static PlayerSnapshot? ParsePlayer(IReadOnlyList<string> tokens, IReadOnlyDictionary<string, int>? headerColumns)
    {
        var player = new PlayerSnapshot
        {
            UserId = tokens.ElementAtOrDefault(1) ?? string.Empty,
            Slot = tokens.ElementAtOrDefault(1) ?? string.Empty
        };

        var nameIndex = GetIndex("name", headerColumns, 2);
        player.Name = tokens.ElementAtOrDefault(nameIndex) ?? string.Empty;

        var uniqueIdIndex = GetIndex("uniqueid", headerColumns, nameIndex + 1);
        var uniqueId = tokens.ElementAtOrDefault(uniqueIdIndex) ?? string.Empty;

        var connectedIndex = GetIndex("connected", headerColumns, uniqueIdIndex + 1);
        var pingIndex = GetIndex("ping", headerColumns, connectedIndex + 1);
        var lossIndex = GetIndex("loss", headerColumns, pingIndex + 1);
        var stateIndex = GetIndex("state", headerColumns, lossIndex + 1);
        var rateIndex = GetIndex("rate", headerColumns, stateIndex + 1);
        var adrIndex = GetIndex("adr", headerColumns, rateIndex + 1);

        player.Connected = tokens.ElementAtOrDefault(connectedIndex) ?? string.Empty;
        player.Ping = tokens.ElementAtOrDefault(pingIndex) ?? string.Empty;
        player.Loss = tokens.ElementAtOrDefault(lossIndex) ?? string.Empty;
        player.State = tokens.ElementAtOrDefault(stateIndex) ?? string.Empty;
        player.Rate = tokens.ElementAtOrDefault(rateIndex) ?? string.Empty;
        player.Address = tokens.ElementAtOrDefault(adrIndex) ?? string.Empty;

        PopulateSteamIdentifiers(player, uniqueId);

        if (string.IsNullOrWhiteSpace(player.Name))
        {
            return null;
        }

        return player;
    }

    private static int GetIndex(string column, IReadOnlyDictionary<string, int>? headerColumns, int fallback)
    {
        if (headerColumns is not null && headerColumns.TryGetValue(column, out var value))
        {
            return value;
        }

        return fallback;
    }

    private static Dictionary<string, int> BuildHeaderColumns(IReadOnlyList<string> tokens)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i].Trim();
            if (token == "#")
            {
                continue;
            }

            map[token] = i;
        }

        return map;
    }

    private static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        foreach (Match match in TokenRegex.Matches(line))
        {
            tokens.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        }

        return tokens;
    }

    private static void PopulateSteamIdentifiers(PlayerSnapshot player, string uniqueId)
    {
        if (string.IsNullOrWhiteSpace(uniqueId))
        {
            return;
        }

        if (uniqueId.StartsWith("STEAM_", StringComparison.OrdinalIgnoreCase))
        {
            player.SteamId = uniqueId;
        }
        else if (uniqueId.StartsWith("[U:", StringComparison.OrdinalIgnoreCase))
        {
            player.Steam3 = uniqueId;
        }
        else if (uniqueId.Length >= 16 && uniqueId.All(char.IsDigit))
        {
            player.SteamId64 = uniqueId;
        }

        if (string.IsNullOrWhiteSpace(player.SteamId) && player.SteamId64 == string.Empty && player.Steam3 == string.Empty)
        {
            player.SteamId = uniqueId;
        }
    }
}

public sealed class PlayerStatusParseResult
{
    public List<PlayerSnapshot> Players { get; } = new();
    public bool HadPlayerLines { get; set; }
}
