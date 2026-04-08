using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CS2AdminTool.Infrastructure;
using CS2AdminTool.Models;
using CS2AdminTool.Services;

namespace CS2AdminTool.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly ConfigLibraryService _configLibraryService;
    private readonly MapLibraryService _mapLibraryService;
    private readonly ConfigRunnerService _runnerService;
    private readonly IRconService _rconService;
    private readonly ServerMonitorService _serverMonitorService;

    private string _host = "127.0.0.1";
    private string _port = "27015";
    private string _password = string.Empty;
    private string _manualCommand = string.Empty;
    private string _newCategoryName = string.Empty;
    private string _configSearchText = string.Empty;
    private string _mapSearchText = string.Empty;
    private string _importPath = string.Empty;
    private string _exportPath = string.Empty;
    private int _commandDelayMs = 250;
    private bool _continueOnFailure;
    private bool _allowBlankCommands;

    private ConfigCategory? _selectedCategory;
    private ServerConfigProfile? _selectedConfig;
    private MapProfile? _selectedMap;
    private ServerConfigProfile? _selectedRecentConfig;

    private AppDataStore _store = new();
    private CancellationTokenSource? _monitorCts;
    private ServerTelemetry _telemetry = new();
    private bool _isAutoRefreshEnabled;
    private int _monitorIntervalSeconds = 5;

    public MainViewModel(ConfigLibraryService configLibraryService, MapLibraryService mapLibraryService, ConfigRunnerService runnerService, IRconService rconService, ServerMonitorService serverMonitorService)
    {
        _configLibraryService = configLibraryService;
        _mapLibraryService = mapLibraryService;
        _runnerService = runnerService;
        _rconService = rconService;
        _serverMonitorService = serverMonitorService;

        Categories = new ObservableCollection<ConfigCategory>();
        Configs = new ObservableCollection<ServerConfigProfile>();
        Maps = new ObservableCollection<MapProfile>();
        RecentConfigs = new ObservableCollection<ServerConfigProfile>();
        LogLines = new ObservableCollection<string>();
        LiveFeedLines = new ObservableCollection<string>();

        ConfigsView = CollectionViewSource.GetDefaultView(Configs);
        ConfigsView.Filter = FilterConfig;
        MapsView = CollectionViewSource.GetDefaultView(Maps);
        MapsView.Filter = FilterMap;

        ToggleConnectionCommand = new AsyncRelayCommand(ToggleConnectionAsync);
        ExecuteCommand = new AsyncRelayCommand(ExecuteManualCommandAsync, () => _rconService.IsConnected);
        RefreshTelemetryCommand = new AsyncRelayCommand(RefreshTelemetryAsync, () => _rconService.IsConnected);
        ToggleAutoRefreshCommand = new AsyncRelayCommand(ToggleAutoRefreshAsync, () => _rconService.IsConnected);

        CreateCategoryCommand = new AsyncRelayCommand(CreateCategoryAsync);
        DeleteCategoryCommand = new AsyncRelayCommand(DeleteSelectedCategoryAsync, () => SelectedCategory is not null);

        CreateConfigCommand = new AsyncRelayCommand(CreateConfigAsync);
        SaveConfigCommand = new AsyncRelayCommand(SaveConfigAsync, () => SelectedConfig is not null);
        DeleteConfigCommand = new AsyncRelayCommand(DeleteSelectedConfigAsync, () => SelectedConfig is not null);
        DuplicateConfigCommand = new AsyncRelayCommand(DuplicateConfigAsync, () => SelectedConfig is not null);
        RunSelectedConfigCommand = new AsyncRelayCommand(RunSelectedConfigAsync, () => SelectedConfig is not null && _rconService.IsConnected);
        RunMapOnlyCommand = new AsyncRelayCommand(RunMapOnlyAsync, () => SelectedConfig is not null && _rconService.IsConnected);

        MoveCommandUpCommand = new RelayCommand(MoveCommandUp, () => SelectedConfig is not null);
        MoveCommandDownCommand = new RelayCommand(MoveCommandDown, () => SelectedConfig is not null);

        CreateMapCommand = new AsyncRelayCommand(CreateMapAsync);
        SaveMapCommand = new AsyncRelayCommand(SaveMapAsync, () => SelectedMap is not null);
        DeleteMapCommand = new AsyncRelayCommand(DeleteSelectedMapAsync, () => SelectedMap is not null);
        DuplicateMapCommand = new AsyncRelayCommand(DuplicateMapAsync, () => SelectedMap is not null);

        ImportAllCommand = new AsyncRelayCommand(ImportAllAsync);
        ExportAllCommand = new AsyncRelayCommand(ExportAllAsync);
        ImportConfigsCommand = new AsyncRelayCommand(ImportConfigsAsync);
        ExportConfigsCommand = new AsyncRelayCommand(ExportConfigsAsync);
        ImportMapsCommand = new AsyncRelayCommand(ImportMapsAsync);
        ExportMapsCommand = new AsyncRelayCommand(ExportMapsAsync);
        ExportSelectedConfigCommand = new AsyncRelayCommand(ExportSelectedConfigAsync, () => SelectedConfig is not null);
        ImportSingleConfigCommand = new AsyncRelayCommand(ImportSingleConfigAsync);

        _ = LoadAsync();
    }

    public ObservableCollection<ConfigCategory> Categories { get; }
    public ObservableCollection<ServerConfigProfile> Configs { get; }
    public ObservableCollection<MapProfile> Maps { get; }
    public ObservableCollection<ServerConfigProfile> RecentConfigs { get; }
    public ObservableCollection<string> LogLines { get; }
    public ObservableCollection<string> LiveFeedLines { get; }

    public ICollectionView ConfigsView { get; }
    public ICollectionView MapsView { get; }

    public string Host { get => _host; set => SetProperty(ref _host, value); }
    public string Port { get => _port; set => SetProperty(ref _port, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string ManualCommand { get => _manualCommand; set => SetProperty(ref _manualCommand, value); }
    public string NewCategoryName { get => _newCategoryName; set => SetProperty(ref _newCategoryName, value); }

    public string ConfigSearchText
    {
        get => _configSearchText;
        set
        {
            if (SetProperty(ref _configSearchText, value))
            {
                ConfigsView.Refresh();
            }
        }
    }

    public string MapSearchText
    {
        get => _mapSearchText;
        set
        {
            if (SetProperty(ref _mapSearchText, value))
            {
                MapsView.Refresh();
            }
        }
    }

    public string ImportPath { get => _importPath; set => SetProperty(ref _importPath, value); }
    public string ExportPath { get => _exportPath; set => SetProperty(ref _exportPath, value); }

    public int CommandDelayMs
    {
        get => _commandDelayMs;
        set
        {
            if (SetProperty(ref _commandDelayMs, value))
            {
                _store.RunnerOptions.CommandDelayMs = value;
            }
        }
    }

    public bool ContinueOnFailure
    {
        get => _continueOnFailure;
        set
        {
            if (SetProperty(ref _continueOnFailure, value))
            {
                _store.RunnerOptions.ContinueOnCommandFailure = value;
            }
        }
    }

    public bool AllowBlankCommands
    {
        get => _allowBlankCommands;
        set
        {
            if (SetProperty(ref _allowBlankCommands, value))
            {
                _store.RunnerOptions.AllowBlankCommands = value;
            }
        }
    }

    public ConfigCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ConfigsView.Refresh();
                RefreshCommandState();
            }
        }
    }

    public ServerConfigProfile? SelectedConfig
    {
        get => _selectedConfig;
        set
        {
            if (SetProperty(ref _selectedConfig, value))
            {
                OnPropertyChanged(nameof(SelectedConfigCommandsText));
                OnPropertyChanged(nameof(SelectedConfigTagsText));
                OnPropertyChanged(nameof(SelectedMapTagsText));
                RefreshCommandState();
            }
        }
    }

    public MapProfile? SelectedMap
    {
        get => _selectedMap;
        set
        {
            if (SetProperty(ref _selectedMap, value))
            {
                OnPropertyChanged(nameof(SelectedMapTagsText));
                RefreshCommandState();
            }
        }
    }

    public ServerConfigProfile? SelectedRecentConfig
    {
        get => _selectedRecentConfig;
        set
        {
            if (SetProperty(ref _selectedRecentConfig, value) && value is not null)
            {
                SelectedConfig = value;
            }
        }
    }

    public string ConnectionButtonText => _rconService.IsConnected ? "Disconnect" : "Connect";

    public IEnumerable<MapProfile> MapOptions => Maps.OrderBy(m => m.DisplayName).ToList();

    public string CurrentMap => _telemetry.CurrentMap;
    public string CurrentGameTypeMode => _telemetry.GameTypeMode;
    public string CurrentServerHostname => _telemetry.ServerHostname;
    public int CurrentPlayerCount => _telemetry.ConnectedPlayerCount;
    public string RawStatusOutput => _telemetry.RawStatus;
    public string RawStatsOutput => _telemetry.RawStats;
    public string LastRefreshTimeText => _telemetry.LastRefreshUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Never";

    public bool IsAutoRefreshEnabled
    {
        get => _isAutoRefreshEnabled;
        set => SetProperty(ref _isAutoRefreshEnabled, value);
    }

    public int MonitorIntervalSeconds
    {
        get => _monitorIntervalSeconds;
        set => SetProperty(ref _monitorIntervalSeconds, value < 1 ? 1 : value);
    }

    public string SelectedMapTagsText
    {
        get => SelectedMap is null ? string.Empty : string.Join(", ", SelectedMap.Tags);
        set
        {
            if (SelectedMap is null)
            {
                return;
            }

            SelectedMap.Tags = value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            OnPropertyChanged();
        }
    }

    public string SelectedConfigTagsText
    {
        get => SelectedConfig is null ? string.Empty : string.Join(", ", SelectedConfig.Tags);
        set
        {
            if (SelectedConfig is null)
            {
                return;
            }

            SelectedConfig.Tags = value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            SelectedConfig.UpdatedAt = DateTime.UtcNow;
            OnPropertyChanged();
        }
    }

    public string SelectedConfigCommandsText
    {
        get => SelectedConfig is null
            ? string.Empty
            : string.Join(Environment.NewLine, SelectedConfig.Commands.OrderBy(c => c.Order).Select(c => c.CommandText));
        set
        {
            if (SelectedConfig is null)
            {
                return;
            }

            var lines = value
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .Select((line, index) => new CommandEntry { Order = index + 1, CommandText = line.Trim() })
                .ToList();

            SelectedConfig.Commands = lines;
            SelectedConfig.UpdatedAt = DateTime.UtcNow;
            OnPropertyChanged();
        }
    }

    public ICommand ToggleConnectionCommand { get; }
    public ICommand ExecuteCommand { get; }
    public ICommand RefreshTelemetryCommand { get; }
    public ICommand ToggleAutoRefreshCommand { get; }
    public ICommand CreateCategoryCommand { get; }
    public ICommand DeleteCategoryCommand { get; }
    public ICommand CreateConfigCommand { get; }
    public ICommand SaveConfigCommand { get; }
    public ICommand DeleteConfigCommand { get; }
    public ICommand DuplicateConfigCommand { get; }
    public ICommand RunSelectedConfigCommand { get; }
    public ICommand RunMapOnlyCommand { get; }
    public ICommand MoveCommandUpCommand { get; }
    public ICommand MoveCommandDownCommand { get; }
    public ICommand CreateMapCommand { get; }
    public ICommand SaveMapCommand { get; }
    public ICommand DeleteMapCommand { get; }
    public ICommand DuplicateMapCommand { get; }
    public ICommand ImportAllCommand { get; }
    public ICommand ExportAllCommand { get; }
    public ICommand ImportConfigsCommand { get; }
    public ICommand ExportConfigsCommand { get; }
    public ICommand ImportMapsCommand { get; }
    public ICommand ExportMapsCommand { get; }
    public ICommand ExportSelectedConfigCommand { get; }
    public ICommand ImportSingleConfigCommand { get; }

    private async Task LoadAsync()
    {
        try
        {
            _store = await _configLibraryService.LoadAsync();

            ApplyCollection(Categories, _store.Categories.OrderBy(c => c.Name));
            ApplyCollection(Maps, _store.Maps.OrderBy(m => m.DisplayName));
            ApplyCollection(Configs, _store.ServerConfigs.OrderBy(c => c.Name));

            CommandDelayMs = _store.RunnerOptions.CommandDelayMs;
            ContinueOnFailure = _store.RunnerOptions.ContinueOnCommandFailure;
            AllowBlankCommands = _store.RunnerOptions.AllowBlankCommands;

            SelectedCategory = Categories.FirstOrDefault();
            SelectedConfig = Configs.FirstOrDefault();
            SelectedMap = Maps.FirstOrDefault();
            AddLog("Configuration libraries loaded.");
        }
        catch (Exception ex)
        {
            AddLog($"[Error] Failed to load libraries: {ex.Message}");
        }
    }

    private async Task PersistAsync()
    {
        _store.Categories = Categories.ToList();
        _store.Maps = Maps.ToList();
        _store.ServerConfigs = Configs.ToList();
        _store.RunnerOptions = new RunnerOptions
        {
            CommandDelayMs = CommandDelayMs,
            ContinueOnCommandFailure = ContinueOnFailure,
            AllowBlankCommands = AllowBlankCommands
        };

        await _configLibraryService.SaveAsync(_store);
        AddLog("Saved data files.");
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

                await _rconService.ConnectAsync(new ServerConfig { Host = Host.Trim(), Port = parsedPort, Password = Password });
                AddLog($"Connected to {Host}:{Port}");
            }

            RefreshCommandState();
            OnPropertyChanged(nameof(ConnectionButtonText));
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
            return;
        }

        try
        {
            AddLog($"> {ManualCommand}");
            var response = await _rconService.SendCommandAsync(ManualCommand.Trim());
            AddLog(response);
            ManualCommand = string.Empty;
        }
        catch (Exception ex)
        {
            AddLog($"[Error] {ex.Message}");
        }
    }

    private async Task RefreshTelemetryAsync()
    {
        if (!_rconService.IsConnected)
        {
            AddLog("[Error] Connect to the server before refreshing telemetry.");
            return;
        }

        try
        {
            var (telemetry, feed) = await _serverMonitorService.RefreshAsync();
            _telemetry = telemetry;
            RaiseTelemetryPropertiesChanged();

            foreach (var line in feed)
            {
                AddLiveFeed(line);
            }

            AddLog("Telemetry refreshed.");
        }
        catch (Exception ex)
        {
            AddLiveFeed($"[{DateTime.Now:HH:mm:ss}] [Monitor][Error] {ex.Message}");
            AddLog($"[Error] Telemetry refresh failed: {ex.Message}");
            StopAutoRefresh();
        }
    }

    private async Task ToggleAutoRefreshAsync()
    {
        if (IsAutoRefreshEnabled)
        {
            StopAutoRefresh();
            return;
        }

        IsAutoRefreshEnabled = true;
        _monitorCts = new CancellationTokenSource();
        AddLog($"Auto-refresh started ({MonitorIntervalSeconds}s interval).");

        try
        {
            while (!_monitorCts.IsCancellationRequested && IsAutoRefreshEnabled)
            {
                await RefreshTelemetryAsync();
                await Task.Delay(TimeSpan.FromSeconds(MonitorIntervalSeconds), _monitorCts.Token);
            }
        }
        catch (TaskCanceledException)
        {
            // expected on stop
        }
        finally
        {
            StopAutoRefresh(false);
        }
    }

    private async Task CreateCategoryAsync()
    {
        var trimmed = NewCategoryName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            AddLog("[Error] Category name is required.");
            return;
        }

        if (Categories.Any(c => c.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            AddLog("[Error] Category already exists.");
            return;
        }

        var category = new ConfigCategory { Name = trimmed, Description = $"{trimmed} configs" };
        Categories.Add(category);
        SelectedCategory = category;
        NewCategoryName = string.Empty;
        await PersistAsync();
    }

    private async Task DeleteSelectedCategoryAsync()
    {
        if (SelectedCategory is null)
        {
            return;
        }

        if (Configs.Any(c => c.Category.Equals(SelectedCategory.Name, StringComparison.OrdinalIgnoreCase)))
        {
            AddLog("[Error] Cannot delete category with assigned configs.");
            return;
        }

        Categories.Remove(SelectedCategory);
        SelectedCategory = Categories.FirstOrDefault();
        await PersistAsync();
    }

    private async Task CreateConfigAsync()
    {
        var category = SelectedCategory?.Name ?? "Custom";
        var config = new ServerConfigProfile
        {
            Name = "New Config",
            Category = category,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Commands = new List<CommandEntry>
            {
                new() { Order = 1, CommandText = "sv_cheats 1" }
            }
        };

        Configs.Add(config);
        SelectedConfig = config;
        ConfigsView.Refresh();
        await PersistAsync();
    }

    private async Task SaveConfigAsync()
    {
        if (SelectedConfig is null)
        {
            return;
        }

        var validationError = ValidateConfig(SelectedConfig);
        if (validationError is not null)
        {
            AddLog($"[Error] {validationError}");
            return;
        }

        SyncConfigMapSnapshot(SelectedConfig);
        SelectedConfig.UpdatedAt = DateTime.UtcNow;

        await PersistAsync();
    }

    private async Task DeleteSelectedConfigAsync()
    {
        if (SelectedConfig is null)
        {
            return;
        }

        Configs.Remove(SelectedConfig);
        SelectedConfig = Configs.FirstOrDefault();
        ConfigsView.Refresh();
        await PersistAsync();
    }

    private async Task DuplicateConfigAsync()
    {
        if (SelectedConfig is null)
        {
            return;
        }

        var duplicate = new ServerConfigProfile
        {
            Name = $"{SelectedConfig.Name} Copy",
            Description = SelectedConfig.Description,
            Category = SelectedConfig.Category,
            MapProfileId = SelectedConfig.MapProfileId,
            IsWorkshopMap = SelectedConfig.IsWorkshopMap,
            WorkshopMapId = SelectedConfig.WorkshopMapId,
            StandardMapName = SelectedConfig.StandardMapName,
            Tags = SelectedConfig.Tags.ToList(),
            Commands = SelectedConfig.Commands.Select(c => new CommandEntry { Order = c.Order, CommandText = c.CommandText }).ToList(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Configs.Add(duplicate);
        SelectedConfig = duplicate;
        ConfigsView.Refresh();
        await PersistAsync();
    }

    private async Task RunSelectedConfigAsync()
    {
        if (SelectedConfig is null)
        {
            return;
        }

        var validationError = ValidateConfig(SelectedConfig);
        if (validationError is not null)
        {
            AddLog($"[Error] {validationError}");
            return;
        }

        try
        {
            var map = ResolveMap(SelectedConfig);
            AddLog($"Running config: {SelectedConfig.Name}");
            var logs = await _runnerService.RunConfigAsync(SelectedConfig, map, _store.RunnerOptions);
            foreach (var line in logs)
            {
                AddLog(line);
            }

            SelectedConfig.LastRunAt = DateTime.UtcNow;
            AddRecent(SelectedConfig);
            await PersistAsync();
        }
        catch (Exception ex)
        {
            AddLog($"[Error] {ex.Message}");
        }
    }

    private async Task RunMapOnlyAsync()
    {
        if (SelectedConfig is null)
        {
            return;
        }

        try
        {
            var map = ResolveMap(SelectedConfig);
            var logs = await _runnerService.RunMapOnlyAsync(SelectedConfig, map, _store.RunnerOptions);
            foreach (var line in logs)
            {
                AddLog(line);
            }
        }
        catch (Exception ex)
        {
            AddLog($"[Error] {ex.Message}");
        }
    }

    private void MoveCommandUp()
    {
        if (SelectedConfig is null || SelectedConfig.Commands.Count < 2)
        {
            return;
        }

        var index = SelectedConfig.Commands.Count - 1;
        if (index <= 0)
        {
            return;
        }

        (SelectedConfig.Commands[index - 1], SelectedConfig.Commands[index]) = (SelectedConfig.Commands[index], SelectedConfig.Commands[index - 1]);
        ReindexCommands(SelectedConfig);
        OnPropertyChanged(nameof(SelectedConfigCommandsText));
    }

    private void MoveCommandDown()
    {
        if (SelectedConfig is null || SelectedConfig.Commands.Count < 2)
        {
            return;
        }

        var index = 0;
        (SelectedConfig.Commands[index], SelectedConfig.Commands[index + 1]) = (SelectedConfig.Commands[index + 1], SelectedConfig.Commands[index]);
        ReindexCommands(SelectedConfig);
        OnPropertyChanged(nameof(SelectedConfigCommandsText));
    }

    private async Task CreateMapAsync()
    {
        var map = new MapProfile
        {
            DisplayName = "New Map",
            Category = SelectedCategory?.Name ?? "Custom",
            StandardMapName = "de_dust2"
        };

        Maps.Add(map);
        SelectedMap = map;
        OnPropertyChanged(nameof(MapOptions));
        await PersistAsync();
    }

    private async Task SaveMapAsync()
    {
        if (SelectedMap is null)
        {
            return;
        }

        var error = ValidateMap(SelectedMap);
        if (error is not null)
        {
            AddLog($"[Error] {error}");
            return;
        }

        await PersistAsync();
    }

    private async Task DeleteSelectedMapAsync()
    {
        if (SelectedMap is null)
        {
            return;
        }

        foreach (var config in Configs.Where(c => c.MapProfileId == SelectedMap.Id))
        {
            config.MapProfileId = null;
        }

        Maps.Remove(SelectedMap);
        SelectedMap = Maps.FirstOrDefault();
        OnPropertyChanged(nameof(MapOptions));
        await PersistAsync();
    }

    private async Task DuplicateMapAsync()
    {
        if (SelectedMap is null)
        {
            return;
        }

        var duplicated = _mapLibraryService.Duplicate(SelectedMap);
        Maps.Add(duplicated);
        SelectedMap = duplicated;
        OnPropertyChanged(nameof(MapOptions));
        await PersistAsync();
    }

    private async Task ImportAllAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportPath) || !File.Exists(ImportPath))
        {
            AddLog("[Error] Import path is invalid.");
            return;
        }

        var imported = await _configLibraryService.ImportAllAsync(ImportPath);
        if (imported is null)
        {
            AddLog("[Error] Failed to import data.");
            return;
        }

        _store = imported;
        ApplyCollection(Categories, _store.Categories);
        ApplyCollection(Maps, _store.Maps);
        ApplyCollection(Configs, _store.ServerConfigs);
        await PersistAsync();
    }

    private async Task ExportAllAsync()
    {
        if (string.IsNullOrWhiteSpace(ExportPath))
        {
            AddLog("[Error] Export path is required.");
            return;
        }

        await _configLibraryService.ExportAllAsync(ExportPath, _store);
        AddLog($"Exported full library: {ExportPath}");
    }

    private async Task ImportConfigsAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportPath) || !File.Exists(ImportPath))
        {
            AddLog("[Error] Import path is invalid.");
            return;
        }

        var configs = await _configLibraryService.ImportConfigsAsync(ImportPath);
        ApplyCollection(Configs, configs);
        await PersistAsync();
    }

    private async Task ExportConfigsAsync()
    {
        if (string.IsNullOrWhiteSpace(ExportPath))
        {
            AddLog("[Error] Export path is required.");
            return;
        }

        await _configLibraryService.ExportConfigsAsync(ExportPath, Configs);
        AddLog($"Exported configs: {ExportPath}");
    }

    private async Task ImportMapsAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportPath) || !File.Exists(ImportPath))
        {
            AddLog("[Error] Import path is invalid.");
            return;
        }

        var maps = await _configLibraryService.ImportMapsAsync(ImportPath);
        ApplyCollection(Maps, maps);
        await PersistAsync();
    }

    private async Task ExportMapsAsync()
    {
        if (string.IsNullOrWhiteSpace(ExportPath))
        {
            AddLog("[Error] Export path is required.");
            return;
        }

        await _configLibraryService.ExportMapsAsync(ExportPath, Maps);
        AddLog($"Exported maps: {ExportPath}");
    }


    private async Task ExportSelectedConfigAsync()
    {
        if (SelectedConfig is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ExportPath))
        {
            AddLog("[Error] Export path is required.");
            return;
        }

        await _configLibraryService.ExportConfigAsync(ExportPath, SelectedConfig);
        AddLog($"Exported selected config: {SelectedConfig.Name}");
    }

    private async Task ImportSingleConfigAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportPath) || !File.Exists(ImportPath))
        {
            AddLog("[Error] Import path is invalid.");
            return;
        }

        var config = await _configLibraryService.ImportConfigAsync(ImportPath);
        if (config is null)
        {
            AddLog("[Error] Failed to import config file.");
            return;
        }

        config.Id = Guid.NewGuid();
        config.UpdatedAt = DateTime.UtcNow;
        Configs.Add(config);
        SelectedConfig = config;
        ConfigsView.Refresh();
        await PersistAsync();
    }

    private bool FilterConfig(object obj)
    {
        if (obj is not ServerConfigProfile config)
        {
            return false;
        }

        var byCategory = SelectedCategory is null || config.Category.Equals(SelectedCategory.Name, StringComparison.OrdinalIgnoreCase);
        var q = ConfigSearchText.Trim();
        var bySearch = string.IsNullOrWhiteSpace(q)
            || config.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || config.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
            || config.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase));

        return byCategory && bySearch;
    }

    private bool FilterMap(object obj)
    {
        if (obj is not MapProfile map)
        {
            return false;
        }

        var q = MapSearchText.Trim();
        return string.IsNullOrWhiteSpace(q)
               || map.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
               || (map.StandardMapName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
               || (map.WorkshopMapId?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
               || map.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    private string? ValidateConfig(ServerConfigProfile config)
    {
        if (string.IsNullOrWhiteSpace(config.Name))
        {
            return "Config name is required.";
        }

        if (string.IsNullOrWhiteSpace(config.Category))
        {
            return "Category is required.";
        }

        var map = ResolveMap(config);
        if (map is not null)
        {
            var mapError = ValidateMap(map);
            if (mapError is not null)
            {
                return mapError;
            }
        }

        if (!AllowBlankCommands && config.Commands.Any(c => string.IsNullOrWhiteSpace(c.CommandText)))
        {
            return "Blank command entries are not allowed.";
        }

        return null;
    }

    private string? ValidateMap(MapProfile map)
    {
        if (string.IsNullOrWhiteSpace(map.DisplayName))
        {
            return "Map display name is required.";
        }

        if (map.IsWorkshopMap && string.IsNullOrWhiteSpace(map.WorkshopMapId))
        {
            return "Workshop map id is required for workshop maps.";
        }

        if (!map.IsWorkshopMap && string.IsNullOrWhiteSpace(map.StandardMapName))
        {
            return "Standard map name is required for non-workshop maps.";
        }

        return null;
    }

    private void SyncConfigMapSnapshot(ServerConfigProfile config)
    {
        var map = ResolveMap(config);
        if (map is null)
        {
            return;
        }

        config.IsWorkshopMap = map.IsWorkshopMap;
        config.WorkshopMapId = map.WorkshopMapId;
        config.StandardMapName = map.StandardMapName;
    }

    private MapProfile? ResolveMap(ServerConfigProfile config)
    {
        if (config.MapProfileId is null)
        {
            return null;
        }

        return Maps.FirstOrDefault(m => m.Id == config.MapProfileId);
    }

    private void ReindexCommands(ServerConfigProfile profile)
    {
        for (var i = 0; i < profile.Commands.Count; i++)
        {
            profile.Commands[i].Order = i + 1;
        }
    }

    private void AddRecent(ServerConfigProfile config)
    {
        var existing = RecentConfigs.FirstOrDefault(c => c.Id == config.Id);
        if (existing is not null)
        {
            RecentConfigs.Remove(existing);
        }

        RecentConfigs.Insert(0, config);
        while (RecentConfigs.Count > 5)
        {
            RecentConfigs.RemoveAt(RecentConfigs.Count - 1);
        }
    }

    private static void ApplyCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    private void RaiseTelemetryPropertiesChanged()
    {
        OnPropertyChanged(nameof(CurrentMap));
        OnPropertyChanged(nameof(CurrentGameTypeMode));
        OnPropertyChanged(nameof(CurrentServerHostname));
        OnPropertyChanged(nameof(CurrentPlayerCount));
        OnPropertyChanged(nameof(RawStatusOutput));
        OnPropertyChanged(nameof(RawStatsOutput));
        OnPropertyChanged(nameof(LastRefreshTimeText));
    }

    private void StopAutoRefresh(bool log = true)
    {
        if (_monitorCts is not null)
        {
            _monitorCts.Cancel();
            _monitorCts.Dispose();
            _monitorCts = null;
        }

        if (IsAutoRefreshEnabled)
        {
            IsAutoRefreshEnabled = false;
            if (log)
            {
                AddLog("Auto-refresh stopped.");
            }
        }
    }

    private void AddLiveFeed(string line)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            LiveFeedLines.Add(line);
            return;
        }

        Application.Current.Dispatcher.Invoke(() => LiveFeedLines.Add(line));
    }

    private void RefreshCommandState()
    {
        (ExecuteCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RefreshTelemetryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ToggleAutoRefreshCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (DeleteCategoryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (SaveConfigCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (DeleteConfigCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (DuplicateConfigCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RunSelectedConfigCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RunMapOnlyCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (MoveCommandUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MoveCommandDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveMapCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (DeleteMapCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (DuplicateMapCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExportSelectedConfigCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private void AddLog(string line)
    {
        var message = $"[{DateTime.Now:HH:mm:ss}] {line}";
        if (Application.Current.Dispatcher.CheckAccess())
        {
            LogLines.Add(message);
            LiveFeedLines.Add(message);
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            LogLines.Add(message);
            LiveFeedLines.Add(message);
        });
    }
}
