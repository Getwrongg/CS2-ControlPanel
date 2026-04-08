using CS2AdminTool.Models;

namespace CS2AdminTool.Services;

public class ConfigRunnerService
{
    private readonly CommandExecutionService _executionService;

    public ConfigRunnerService(CommandExecutionService executionService)
    {
        _executionService = executionService;
    }

    public async Task<IReadOnlyList<string>> RunConfigAsync(ServerConfigProfile config, MapProfile? map, RunnerOptions options, CancellationToken cancellationToken = default)
    {
        var commands = new List<string>();

        var mapCommand = BuildMapChangeCommand(config, map);
        if (!string.IsNullOrWhiteSpace(mapCommand))
        {
            commands.Add(mapCommand);
        }

        commands.AddRange(config.Commands
            .OrderBy(c => c.Order)
            .Select(c => c.CommandText));

        return await _executionService.ExecuteSequentiallyAsync(commands, options, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> RunMapOnlyAsync(ServerConfigProfile config, MapProfile? map, RunnerOptions options, CancellationToken cancellationToken = default)
    {
        var mapCommand = BuildMapChangeCommand(config, map);
        if (string.IsNullOrWhiteSpace(mapCommand))
        {
            return ["[Info] No map configured for this profile."];
        }

        return await _executionService.ExecuteSequentiallyAsync([mapCommand], options, cancellationToken);
    }

    private static string? BuildMapChangeCommand(ServerConfigProfile config, MapProfile? map)
    {
        if (map is not null)
        {
            var mapCommand = TryBuildMapCommand(map.IsWorkshopMap, map.WorkshopMapId, map.StandardMapName);
            if (!string.IsNullOrWhiteSpace(mapCommand))
            {
                return mapCommand;
            }
        }

        return TryBuildMapCommand(config.IsWorkshopMap, config.WorkshopMapId, config.StandardMapName);
    }

    private static string? TryBuildMapCommand(bool isWorkshopMap, string? workshopMapId, string? standardMapName)
    {
        if (isWorkshopMap)
        {
            return string.IsNullOrWhiteSpace(workshopMapId)
                ? null
                : $"host_workshop_map {workshopMapId}";
        }

        return string.IsNullOrWhiteSpace(standardMapName)
            ? null
            : $"changelevel {standardMapName}";
    }
}
