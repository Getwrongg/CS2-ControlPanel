using System.Net;
using CoreRCON;
using CS2AdminTool.Models;

namespace CS2AdminTool.Services;

public class RconService : IRconService
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private RCON? _client;
    private ServerConfig? _currentConfig;

    public bool IsConnected { get; private set; }

    public async Task ConnectAsync(ServerConfig config, CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
            {
                return;
            }

            _currentConfig = new ServerConfig
            {
                Host = config.Host,
                Port = config.Port,
                Password = config.Password
            };

            var addresses = await Dns.GetHostAddressesAsync(config.Host, cancellationToken);
            var ip = addresses.FirstOrDefault() ?? throw new InvalidOperationException("Unable to resolve host.");

            _client = new RCON(ip, (ushort)config.Port, config.Password);
            await _client.ConnectAsync();
            IsConnected = true;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_client is not null)
            {
                _client.Dispose();
                _client = null;
            }

            IsConnected = false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command cannot be empty.", nameof(command));
        }

        await EnsureConnectedAsync(cancellationToken);

        if (_client is null)
        {
            throw new InvalidOperationException("RCON client is not initialized.");
        }

        try
        {
            return await _client.SendCommandAsync(command);
        }
        catch
        {
            IsConnected = false;
            await EnsureConnectedAsync(cancellationToken);

            if (_client is null)
            {
                throw;
            }

            return await _client.SendCommandAsync(command);
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return;
        }

        if (_currentConfig is null)
        {
            throw new InvalidOperationException("Not connected. Configure and connect first.");
        }

        await ConnectAsync(_currentConfig, cancellationToken);
    }
}
