using CS2AdminTool.Models;

namespace CS2AdminTool.Services;

public sealed class PlayerHistoryService
{
    private static readonly TimeSpan SeenWindow = TimeSpan.FromMinutes(2);

    public bool MergePlayers(IList<PlayerHistoryEntry> history, IEnumerable<PlayerSnapshot> players, string host, int port, DateTime seenUtc)
    {
        var changed = false;
        var mergedThisCycle = new HashSet<Guid>();

        foreach (var player in players)
        {
            var entry = FindMatch(history, player);
            if (entry is null)
            {
                history.Add(CreateEntry(player, host, port, seenUtc));
                changed = true;
                continue;
            }

            var entryChanged = false;
            var previousLastSeenUtc = entry.LastSeenUtc;
            entryChanged |= UpdateIfDifferent(entry.LastKnownName, player.Name, value => entry.LastKnownName = value);
            entryChanged |= UpdateIfDifferent(entry.SteamId, player.SteamId, value => entry.SteamId = value);
            entryChanged |= UpdateIfDifferent(entry.SteamId64, player.SteamId64, value => entry.SteamId64 = value);
            entryChanged |= UpdateIfDifferent(entry.Steam3, player.Steam3, value => entry.Steam3 = value);
            entryChanged |= UpdateIfDifferent(entry.LastKnownIp, NormalizeIp(player.Address), value => entry.LastKnownIp = value);
            entryChanged |= UpdateIfDifferent(entry.LastServerHost, host, value => entry.LastServerHost = value);
            if (entry.LastServerPort != port)
            {
                entry.LastServerPort = port;
                entryChanged = true;
            }

            if (entry.LastSeenUtc < seenUtc)
            {
                entry.LastSeenUtc = seenUtc;
                entryChanged = true;
            }

            if (!mergedThisCycle.Contains(entry.Id) && seenUtc - previousLastSeenUtc >= SeenWindow)
            {
                entry.TimesSeen++;
                entryChanged = true;
            }

            mergedThisCycle.Add(entry.Id);
            changed |= entryChanged;
        }

        return changed;
    }

    private static PlayerHistoryEntry CreateEntry(PlayerSnapshot player, string host, int port, DateTime seenUtc)
    {
        return new PlayerHistoryEntry
        {
            LastKnownName = player.Name,
            SteamId = player.SteamId,
            SteamId64 = player.SteamId64,
            Steam3 = player.Steam3,
            LastKnownIp = NormalizeIp(player.Address),
            FirstSeenUtc = seenUtc,
            LastSeenUtc = seenUtc,
            LastServerHost = host,
            LastServerPort = port,
            TimesSeen = 1
        };
    }

    private static PlayerHistoryEntry? FindMatch(IEnumerable<PlayerHistoryEntry> history, PlayerSnapshot player)
    {
        if (!string.IsNullOrWhiteSpace(player.SteamId64))
        {
            return history.FirstOrDefault(h => h.SteamId64.Equals(player.SteamId64, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(player.SteamId))
        {
            return history.FirstOrDefault(h => h.SteamId.Equals(player.SteamId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(player.Steam3))
        {
            return history.FirstOrDefault(h => h.Steam3.Equals(player.Steam3, StringComparison.OrdinalIgnoreCase));
        }

        var ip = NormalizeIp(player.Address);
        if (!string.IsNullOrWhiteSpace(ip))
        {
            return history.FirstOrDefault(h => h.LastKnownIp.Equals(ip, StringComparison.OrdinalIgnoreCase)
                                               && h.LastKnownName.Equals(player.Name, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static bool UpdateIfDifferent(string target, string source, Action<string> updateAction)
    {
        if (string.IsNullOrWhiteSpace(source) || target.Equals(source, StringComparison.Ordinal))
        {
            return false;
        }

        updateAction(source);
        return true;
    }

    public static string NormalizeIp(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return string.Empty;
        }

        var withoutPort = address.Split(':', StringSplitOptions.TrimEntries)[0];
        return withoutPort;
    }
}
