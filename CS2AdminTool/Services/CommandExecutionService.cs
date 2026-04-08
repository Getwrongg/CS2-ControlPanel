using CS2AdminTool.Models;

namespace CS2AdminTool.Services;

public class CommandExecutionService
{
    private readonly IRconService _rconService;
    private readonly SemaphoreSlim _commandLock = new(1, 1);

    public CommandExecutionService(IRconService rconService)
    {
        _rconService = rconService;
    }

    public async Task<IReadOnlyList<string>> ExecuteSequentiallyAsync(IEnumerable<string> commands, RunnerOptions options, CancellationToken cancellationToken = default)
    {
        var logs = new List<string>();
        await _commandLock.WaitAsync(cancellationToken);

        try
        {
            foreach (var rawCommand in commands)
            {
                var command = rawCommand.Trim();
                if (string.IsNullOrWhiteSpace(command))
                {
                    if (options.AllowBlankCommands)
                    {
                        continue;
                    }

                    throw new InvalidOperationException("Blank command entries are not allowed.");
                }

                logs.Add($"> {command}");

                try
                {
                    var response = await _rconService.SendCommandAsync(command, cancellationToken);
                    logs.Add(response);
                }
                catch (Exception ex)
                {
                    logs.Add($"[Error] {ex.Message}");
                    if (!options.ContinueOnCommandFailure)
                    {
                        throw;
                    }
                }

                if (options.CommandDelayMs > 0)
                {
                    await Task.Delay(options.CommandDelayMs, cancellationToken);
                }
            }
        }
        finally
        {
            _commandLock.Release();
        }

        return logs;
    }
}
