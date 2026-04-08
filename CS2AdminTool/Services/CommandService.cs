using System.Text.Json;
using CS2AdminTool.Models;
using System.IO;

namespace CS2AdminTool.Services;

public class CommandService
{
    private readonly IRconService _rconService;
    private readonly SemaphoreSlim _commandLock = new(1, 1);

    public CommandService(IRconService rconService)
    {
        _rconService = rconService;
    }

    public async Task<IReadOnlyList<CommandPreset>> LoadPresetsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var presets = await JsonSerializer.DeserializeAsync<List<CommandPreset>>(stream, cancellationToken: cancellationToken);
        return presets ?? [];
    }

    public async Task<string> RunSingleCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        await _commandLock.WaitAsync(cancellationToken);
        try
        {
            return await _rconService.SendCommandAsync(command, cancellationToken);
        }
        finally
        {
            _commandLock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> RunPresetAsync(CommandPreset preset, CancellationToken cancellationToken = default)
    {
        var results = new List<string>();

        await _commandLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var command in preset.Commands)
            {
                var response = await _rconService.SendCommandAsync(command, cancellationToken);
                results.Add($"> {command}");
                results.Add(response);
            }
        }
        finally
        {
            _commandLock.Release();
        }

        return results;
    }
}
