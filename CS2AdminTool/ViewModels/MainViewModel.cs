using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CS2AdminTool.Infrastructure;
using CS2AdminTool.Models;
using CS2AdminTool.Services;
using System.IO;

namespace CS2AdminTool.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly CommandService _commandService;
    private readonly IRconService _rconService;

    private string _host = "127.0.0.1";
    private string _port = "27015";
    private string _password = string.Empty;
    private string _manualCommand = string.Empty;

    public MainViewModel(CommandService commandService, IRconService rconService)
    {
        _commandService = commandService;
        _rconService = rconService;

        Presets = new ObservableCollection<CommandPreset>();
        LogLines = new ObservableCollection<string>();

        ToggleConnectionCommand = new AsyncRelayCommand(ToggleConnectionAsync);
        ExecuteCommand = new AsyncRelayCommand(ExecuteManualCommandAsync, () => _rconService.IsConnected);
        RunPresetCommand = new AsyncRelayCommand<CommandPreset>(RunPresetAsync, preset => preset is not null && _rconService.IsConnected);

        _ = LoadPresetsAsync();
    }

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public string Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ManualCommand
    {
        get => _manualCommand;
        set => SetProperty(ref _manualCommand, value);
    }

    public string ConnectionButtonText => _rconService.IsConnected ? "Disconnect" : "Connect";

    public ObservableCollection<CommandPreset> Presets { get; }
    public ObservableCollection<string> LogLines { get; }

    public ICommand ToggleConnectionCommand { get; }
    public ICommand ExecuteCommand { get; }
    public ICommand RunPresetCommand { get; }

    private async Task LoadPresetsAsync()
    {
        try
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, "Data", "presets.json");
            var presets = await _commandService.LoadPresetsAsync(filePath);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Presets.Clear();
                foreach (var preset in presets)
                {
                    Presets.Add(preset);
                }
            });
        }
        catch (Exception ex)
        {
            AddLog($"[Error] Failed to load presets: {ex.Message}");
        }
    }

    private async Task ToggleConnectionAsync()
    {
        try
        {
            if (_rconService.IsConnected)
            {
                await _rconService.DisconnectAsync();
                AddLog("Disconnected from server.");
            }
            else
            {
                if (!int.TryParse(Port, out var parsedPort))
                {
                    AddLog("[Error] Port must be a valid integer.");
                    return;
                }

                var config = new ServerConfig
                {
                    Host = Host.Trim(),
                    Port = parsedPort,
                    Password = Password
                };

                await _rconService.ConnectAsync(config);
                AddLog($"Connected to {config.Host}:{config.Port}");
            }

            RefreshCommandState();
        }
        catch (Exception ex)
        {
            AddLog($"[Error] {ex.Message}");
        }
    }

    private async Task ExecuteManualCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualCommand))
        {
            AddLog("[Warning] Enter a command before executing.");
            return;
        }

        try
        {
            AddLog($"> {ManualCommand}");
            var response = await _commandService.RunSingleCommandAsync(ManualCommand.Trim());
            AddLog(response);
            ManualCommand = string.Empty;
        }
        catch (Exception ex)
        {
            AddLog($"[Error] {ex.Message}");
        }
    }

    private async Task RunPresetAsync(CommandPreset? preset)
    {
        if (preset is null)
        {
            return;
        }

        try
        {
            AddLog($"Running preset: {preset.Name}");
            var results = await _commandService.RunPresetAsync(preset);
            foreach (var line in results)
            {
                AddLog(line);
            }

            AddLog($"Finished preset: {preset.Name}");
        }
        catch (Exception ex)
        {
            AddLog($"[Error] {ex.Message}");
        }
    }

    private void RefreshCommandState()
    {
        OnPropertyChanged(nameof(ConnectionButtonText));
        if (ExecuteCommand is AsyncRelayCommand execute)
        {
            execute.RaiseCanExecuteChanged();
        }

        if (RunPresetCommand is AsyncRelayCommand<CommandPreset> runPreset)
        {
            runPreset.RaiseCanExecuteChanged();
        }
    }

    private void AddLog(string line)
    {
        var formatted = $"[{DateTime.Now:HH:mm:ss}] {line}";

        if (Application.Current.Dispatcher.CheckAccess())
        {
            LogLines.Add(formatted);
            return;
        }

        Application.Current.Dispatcher.Invoke(() => LogLines.Add(formatted));
    }
}
