using CS2AdminTool.Models;

namespace CS2AdminTool.Services;

public interface IRconService
{
    bool IsConnected { get; }
    Task ConnectAsync(ServerConfig config, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default);
}
