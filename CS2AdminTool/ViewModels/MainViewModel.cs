using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly PlayerStatusParser _playerStatusParser;
    private readonly PlayerHistoryService _playerHistoryService;

    private string _host = "127.0.0.1";
    private string _port = "27015";
    private string _password = string.Empty;
    private string _savedServerName = string.Empty;
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
    private SavedServerProfile? _selectedSavedServer;

    private AppDataStore _store = new();
    private CancellationTokenSource? _monitorCts;
    private ServerTelemetry _telemetry = new();
    private bool _isAutoRefreshEnabled;
    private int _monitorIntervalSeconds = 5;
    private readonly Stack<PresetCommandPack> _appliedPresetHistory = new();
    private CancellationTokenSource? _automationCts;
    private string _scheduleMinutes = "30";
    private string _scheduleAction = "Warmup Pack";
    private bool _dryRunMode = true;
    private string _commandDenylist = "quit,restart,exec autoexec.cfg";
    private string _commandAllowlist = string.Empty;
    private bool _requireDestructiveConfirm = true;
    private string _playerActionReason = "admin action";
    private string _playerHistorySearchText = string.Empty;
    private string _playerHistoryExportPath = "Data/Exports/player-history.json";
    private string _lastPlayerParseSource = string.Empty;
    private bool? _isSourceModAvailable;
    private PlayerSnapshot? _selectedPlayer;
    private PlayerHistoryEntry? _selectedHistoryPlayer;
    private PresetCommandPack? _selectedPresetPack;
    private string _auditExportPath = "Data/Exports/audit-log.json";
    private ServerHealthSnapshot _serverHealth = new();

    public MainViewModel(
        ConfigLibraryService configLibraryService,
        MapLibraryService mapLibraryService,
        ConfigRunnerService runnerService,
        IRconService rconService,
        ServerMonitorService serverMonitorService,
        PlayerStatusParser playerStatusParser,
        PlayerHistoryService playerHistoryService)
    {
        _configLibraryService = configLibraryService;
        _mapLibraryService = mapLibraryService;
        _runnerService = runnerService;
        _rconService = rconService;
        _serverMonitorService = serverMonitorService;
        _playerStatusParser = playerStatusParser;
        _playerHistoryService = playerHistoryService;

        Categories = new ObservableCollection<ConfigCategory>();
        Configs = new ObservableCollection<ServerConfigProfile>();
        Maps = new ObservableCollection<MapProfile>();
        RecentConfigs = new ObservableCollection<ServerConfigProfile>();
        SavedServers = new ObservableCollection<SavedServerProfile>();
        LogLines = new ObservableCollection<string>();
        LiveFeedLines = new ObservableCollection<string>();
        PresetPacks = new ObservableCollection<PresetCommandPack>(BuildDefaultPresetPacks());
        ParsedPlayers = new ObservableCollection<PlayerSnapshot>();
        PlayerHistory = new ObservableCollection<PlayerHistoryEntry>();
        AuditLogEntries = new ObservableCollection<AuditLogEntry>();

        ConfigsView = CollectionViewSource.GetDefaultView(Configs);
        ConfigsView.Filter = FilterConfig;
        MapsView = CollectionViewSource.GetDefaultView(Maps);
        MapsView.Filter = FilterMap;
        PlayerHistoryView = CollectionViewSource.GetDefaultView(PlayerHistory);
        PlayerHistoryView.Filter = FilterPlayerHistory;

        ToggleConnectionCommand = new AsyncRelayCommand(ToggleConnectionAsync);
        SaveServerProfileCommand = new AsyncRelayCommand(SaveServerProfileAsync);
        DeleteServerProfileCommand = new AsyncRelayCommand(DeleteSelectedServerProfileAsync, () => SelectedSavedServer is not null);
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
        RunPresetPackCommand = new AsyncRelayCommand(RunSelectedPresetPackAsync, () => _rconService.IsConnected && SelectedPresetPack is not null);
        RollbackPresetPackCommand = new AsyncRelayCommand(RollbackLastPresetPackAsync, () => _rconService.IsConnected && _appliedPresetHistory.Count > 0);
        RefreshPlayersCommand = new AsyncRelayCommand(RefreshPlayersAsync, () => _rconService.IsConnected);
        KickPlayerCommand = new AsyncRelayCommand(KickSelectedPlayerAsync, () => _rconService.IsConnected && SelectedPlayer is not null);
        BanPlayerCommand = new AsyncRelayCommand(BanSelectedPlayerAsync, () => _rconService.IsConnected && SelectedPlayer is not null);
        MutePlayerCommand = new AsyncRelayCommand(MuteSelectedPlayerAsync, () => _rconService.IsConnected && SelectedPlayer is not null);
        SwapPlayerTeamCommand = new AsyncRelayCommand(SwapSelectedPlayerTeamAsync, () => _rconService.IsConnected && SelectedPlayer is not null);
        CopyPlayerSteamIdCommand = new RelayCommand(CopySelectedHistorySteamId, () => SelectedHistoryPlayer is not null);
        CopyPlayerIpCommand = new RelayCommand(CopySelectedHistoryIp, () => SelectedHistoryPlayer is not null);
        ExportPlayerHistoryCommand = new AsyncRelayCommand(ExportPlayerHistoryAsync, () => PlayerHistory.Count > 0);
        UnbanPlayerByIpCommand = new AsyncRelayCommand(UnbanSelectedPlayerByIpAsync, () => _rconService.IsConnected && SelectedHistoryPlayer is not null);
        ReadyCheckCommand = new AsyncRelayCommand(RunReadyCheckAsync, () => _rconService.IsConnected);
        ToggleAutomationCommand = new AsyncRelayCommand(ToggleScheduledAutomationAsync, () => _rconService.IsConnected);
        RunPreMatchChecklistCommand = new AsyncRelayCommand(RunPreMatchChecklistAsync, () => _rconService.IsConnected);
        RunHalftimeSwitchCommand = new AsyncRelayCommand(RunHalftimeSwitchAsync, () => _rconService.IsConnected);
        RunPostMatchArchiveCommand = new AsyncRelayCommand(RunPostMatchArchiveAsync, () => _rconService.IsConnected);
        ExportAuditLogCommand = new AsyncRelayCommand(ExportAuditLogAsync);
        ClearServerOverridesCommand = new AsyncRelayCommand(ClearServerOverridesAsync, () => _rconService.IsConnected);

        _ = LoadAsync();
    }

    public ObservableCollection<ConfigCategory> Categories { get; }
    public ObservableCollection<ServerConfigProfile> Configs { get; }
    public ObservableCollection<MapProfile> Maps { get; }
    public ObservableCollection<ServerConfigProfile> RecentConfigs { get; }
    public ObservableCollection<SavedServerProfile> SavedServers { get; }
    public ObservableCollection<string> LogLines { get; }
    public ObservableCollection<string> LiveFeedLines { get; }
    public ObservableCollection<PresetCommandPack> PresetPacks { get; }
    public ObservableCollection<PlayerSnapshot> ParsedPlayers { get; }
    public ObservableCollection<PlayerHistoryEntry> PlayerHistory { get; }
    public ObservableCollection<AuditLogEntry> AuditLogEntries { get; }

    public ICollectionView ConfigsView { get; }
    public ICollectionView MapsView { get; }
    public ICollectionView PlayerHistoryView { get; }

    public string Host { get => _host; set => SetProperty(ref _host, value); }
    public string Port { get => _port; set => SetProperty(ref _port, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string SavedServerName { get => _savedServerName; set => SetProperty(ref _savedServerName, value); }
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

    public SavedServerProfile? SelectedSavedServer
    {
        get => _selectedSavedServer;
        set
        {
            if (SetProperty(ref _selectedSavedServer, value))
            {
                if (value is not null)
                {
                    SavedServerName = value.Name;
                    Host = value.Host;
                    Port = value.Port.ToString();
                    Password = value.Password;
                }

                RefreshCommandState();
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

    public PresetCommandPack? SelectedPresetPack
    {
        get => _selectedPresetPack;
        set
        {
            if (SetProperty(ref _selectedPresetPack, value))
            {
                RefreshCommandState();
            }
        }
    }
    public PlayerSnapshot? SelectedPlayer
    {
        get => _selectedPlayer;
        set
        {
            if (SetProperty(ref _selectedPlayer, value))
            {
                RefreshCommandState();
            }
        }
    }

    public PlayerHistoryEntry? SelectedHistoryPlayer
    {
        get => _selectedHistoryPlayer;
        set
        {
            if (SetProperty(ref _selectedHistoryPlayer, value))
            {
                RefreshCommandState();
            }
        }
    }

    public string PlayerHistorySearchText
    {
        get => _playerHistorySearchText;
        set
        {
            if (SetProperty(ref _playerHistorySearchText, value))
            {
                PlayerHistoryView.Refresh();
            }
        }
    }

    public string ScheduleMinutes { get => _scheduleMinutes; set => SetProperty(ref _scheduleMinutes, value); }
    public string ScheduleAction { get => _scheduleAction; set => SetProperty(ref _scheduleAction, value); }
    public bool DryRunMode { get => _dryRunMode; set => SetProperty(ref _dryRunMode, value); }
    public string CommandDenylist { get => _commandDenylist; set => SetProperty(ref _commandDenylist, value); }
    public string CommandAllowlist { get => _commandAllowlist; set => SetProperty(ref _commandAllowlist, value); }
    public bool RequireDestructiveConfirm { get => _requireDestructiveConfirm; set => SetProperty(ref _requireDestructiveConfirm, value); }
    public string PlayerActionReason { get => _playerActionReason; set => SetProperty(ref _playerActionReason, value); }
    public string PlayerHistoryExportPath { get => _playerHistoryExportPath; set => SetProperty(ref _playerHistoryExportPath, value); }
    public string LastPlayerParseSource { get => _lastPlayerParseSource; set => SetProperty(ref _lastPlayerParseSource, value); }
    public string AuditExportPath { get => _auditExportPath; set => SetProperty(ref _auditExportPath, value); }
    public bool IsAutomationEnabled => _automationCts is not null;

    public double TickRateVariance => _serverHealth.TickRateVariance;
    public double ChokePercent => _serverHealth.ChokePercent;
    public double LossPercent => _serverHealth.LossPercent;
    public int BotCount => _serverHealth.BotCount;
    public string HealthAlert => _serverHealth.Alert;

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
    public ICommand RunPresetPackCommand { get; }
    public ICommand RollbackPresetPackCommand { get; }
    public ICommand RefreshPlayersCommand { get; }
    public ICommand KickPlayerCommand { get; }
    public ICommand BanPlayerCommand { get; }
    public ICommand MutePlayerCommand { get; }
    public ICommand SwapPlayerTeamCommand { get; }
    public ICommand CopyPlayerSteamIdCommand { get; }
    public ICommand CopyPlayerIpCommand { get; }
    public ICommand ExportPlayerHistoryCommand { get; }
    public ICommand UnbanPlayerByIpCommand { get; }
    public ICommand ReadyCheckCommand { get; }
    public ICommand ToggleAutomationCommand { get; }
    public ICommand RunPreMatchChecklistCommand { get; }
    public ICommand RunHalftimeSwitchCommand { get; }
    public ICommand RunPostMatchArchiveCommand { get; }
    public ICommand ExportAuditLogCommand { get; }
    public ICommand ClearServerOverridesCommand { get; }
    public ICommand SaveServerProfileCommand { get; }
    public ICommand DeleteServerProfileCommand { get; }

    private async Task LoadAsync()
    {
        try
        {
            _store = await _configLibraryService.LoadAsync();

            ApplyCollection(Categories, _store.Categories.OrderBy(c => c.Name));
            ApplyCollection(Maps, _store.Maps.OrderBy(m => m.DisplayName));
            ApplyCollection(Configs, _store.ServerConfigs.OrderBy(c => c.Name));
            ApplyCollection(SavedServers, _store.SavedServers.OrderBy(s => s.Name));
            ApplyCollection(PlayerHistory, _store.PlayerHistory.OrderByDescending(p => p.LastSeenUtc));

            CommandDelayMs = _store.RunnerOptions.CommandDelayMs;
            ContinueOnFailure = _store.RunnerOptions.ContinueOnCommandFailure;
            AllowBlankCommands = _store.RunnerOptions.AllowBlankCommands;

            SelectedCategory = Categories.FirstOrDefault();
            SelectedConfig = Configs.FirstOrDefault();
            SelectedMap = Maps.FirstOrDefault();
            SelectedSavedServer = SavedServers.FirstOrDefault();
            SelectedHistoryPlayer = PlayerHistory.FirstOrDefault();
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
        _store.SavedServers = SavedServers.OrderBy(s => s.Name).ToList();
        _store.PlayerHistory = PlayerHistory.OrderByDescending(p => p.LastSeenUtc).ToList();
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
                _isSourceModAvailable = null;
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
                _isSourceModAvailable = null;
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

    private async Task SaveServerProfileAsync()
    {
        if (!int.TryParse(Port, out var parsedPort))
        {
            AddLog("[Error] Port must be a valid integer.");
            return;
        }

        var trimmedHost = Host.Trim();
        if (string.IsNullOrWhiteSpace(trimmedHost))
        {
            AddLog("[Error] Host is required.");
            return;
        }

        var trimmedName = SavedServerName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            trimmedName = $"{trimmedHost}:{parsedPort}";
        }

        var profile = SelectedSavedServer;
        if (profile is null)
        {
            profile = SavedServers.FirstOrDefault(s => s.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
        }

        if (profile is null)
        {
            profile = new SavedServerProfile();
            SavedServers.Add(profile);
        }

        profile.Name = trimmedName;
        profile.Host = trimmedHost;
        profile.Port = parsedPort;
        profile.Password = Password;

        ResortSavedServers();
        SelectedSavedServer = profile;

        await PersistAsync();
        AddLog($"Saved server profile '{profile.Name}'.");
    }

    private async Task DeleteSelectedServerProfileAsync()
    {
        if (SelectedSavedServer is null)
        {
            return;
        }

        var profileName = SelectedSavedServer.Name;
        SavedServers.Remove(SelectedSavedServer);
        SelectedSavedServer = SavedServers.FirstOrDefault();
        if (SelectedSavedServer is null)
        {
            SavedServerName = string.Empty;
        }

        await PersistAsync();
        AddLog($"Deleted server profile '{profileName}'.");
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
            _serverHealth = BuildHealthSnapshot(telemetry.RawStats, telemetry.RawStatus);
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

    private async Task RunSelectedPresetPackAsync()
    {
        if (SelectedPresetPack is null)
        {
            AddLog("[Error] Select a preset pack first.");
            return;
        }

        var commands = SelectedPresetPack.Commands.Where(CanExecuteCommandSafely).ToList();
        if (commands.Count == 0)
        {
            AddLog("[Info] No commands passed guardrails.");
            return;
        }

        foreach (var command in commands)
        {
            await ExecuteAndAuditAsync($"Preset:{SelectedPresetPack.Name}", command);
        }

        _appliedPresetHistory.Push(SelectedPresetPack);
        AddLog($"Preset applied: {SelectedPresetPack.Name}");
        RefreshCommandState();
    }

    private async Task RollbackLastPresetPackAsync()
    {
        if (_appliedPresetHistory.Count == 0)
        {
            AddLog("[Info] No applied preset to rollback.");
            return;
        }

        var preset = _appliedPresetHistory.Pop();
        foreach (var command in preset.RollbackCommands.Where(CanExecuteCommandSafely))
        {
            await ExecuteAndAuditAsync($"PresetRollback:{preset.Name}", command);
        }

        AddLog($"Rollback applied: {preset.Name}");
        RefreshCommandState();
    }

    private async Task RefreshPlayersAsync()
    {
        AddLog("[Ops+] Player refresh started.");
        var status = await _rconService.SendCommandAsync("status");
        LastPlayerParseSource = status;
        AddLog($"[Ops+] Raw status received ({status.Length} chars).");

        var parsed = _playerStatusParser.Parse(status);
        ApplyCollection(ParsedPlayers, parsed.Players);
        SelectedPlayer = ParsedPlayers.FirstOrDefault();

        if (parsed.Players.Count == 0 && parsed.HadPlayerLines)
        {
            AddLog("[Warning] Parser found player-like rows but parsed zero players.");
        }

        AddLog($"[Ops+] Parsing completed. Parsed players: {ParsedPlayers.Count}.");
        await MergePlayerHistoryAsync(parsed.Players);
    }

    private async Task KickSelectedPlayerAsync()
    {
        if (SelectedPlayer is null || string.IsNullOrWhiteSpace(SelectedPlayer.UserId))
        {
            AddLog("[Error] Kick requires a valid userid from status output.");
            return;
        }

        await ExecuteAndAuditAsync("PlayerKick", $"kickid {SelectedPlayer.UserId} \"{PlayerActionReason}\"");
    }

    private async Task BanSelectedPlayerAsync()
    {
        if (SelectedPlayer is null)
        {
            AddLog("[Error] Select a player first.");
            return;
        }

        var banIdentifier = !string.IsNullOrWhiteSpace(SelectedPlayer.SteamId64)
            ? SelectedPlayer.SteamId64
            : !string.IsNullOrWhiteSpace(SelectedPlayer.SteamId)
                ? SelectedPlayer.SteamId
                : SelectedPlayer.Steam3;

        if (string.IsNullOrWhiteSpace(banIdentifier))
        {
            AddLog("[Error] Ban requires Steam identity (SteamID/SteamID64/Steam3).");
            return;
        }

        await ExecuteAndAuditAsync("PlayerBan", $"banid 0 {banIdentifier}");
        await ExecuteAndAuditAsync("PlayerBan", "writeid");
    }

    private async Task MuteSelectedPlayerAsync()
    {
        if (!await EnsureSourceModCommandAvailableAsync("sm_mute"))
        {
            return;
        }

        if (SelectedPlayer is null || string.IsNullOrWhiteSpace(SelectedPlayer.UserId))
        {
            AddLog("[Error] Mute requires a valid userid from status output.");
            return;
        }

        await ExecuteAndAuditAsync("PlayerMute", $"sm_mute #{SelectedPlayer.UserId}");
    }

    private async Task SwapSelectedPlayerTeamAsync()
    {
        if (!await EnsureSourceModCommandAvailableAsync("sm_swap"))
        {
            return;
        }

        if (SelectedPlayer is null || string.IsNullOrWhiteSpace(SelectedPlayer.UserId))
        {
            AddLog("[Error] Team swap requires a valid userid from status output.");
            return;
        }

        await ExecuteAndAuditAsync("PlayerSwapTeam", $"sm_swap #{SelectedPlayer.UserId}");
    }

    private async Task MergePlayerHistoryAsync(IEnumerable<PlayerSnapshot> players)
    {
        var seenUtc = DateTime.UtcNow;
        var port = int.TryParse(Port, out var parsedPort) ? parsedPort : 0;
        var changed = _playerHistoryService.MergePlayers(PlayerHistory, players, Host, port, seenUtc);
        var sortedHistory = PlayerHistory.OrderByDescending(p => p.LastSeenUtc).ToList();
        ApplyCollection(PlayerHistory, sortedHistory);
        PlayerHistoryView.Refresh();

        if (changed)
        {
            AddLog("[Ops+] Player history updated.");
            await PersistAsync();
            AddLog("[Ops+] Player history saved.");
        }
    }

    private async Task<bool> EnsureSourceModCommandAvailableAsync(string command)
    {
        if (_isSourceModAvailable is null)
        {
            var probe = await _rconService.SendCommandAsync("sm version");
            _isSourceModAvailable = !probe.Contains("Unknown command", StringComparison.OrdinalIgnoreCase)
                                    && !probe.Contains("not found", StringComparison.OrdinalIgnoreCase);

            AddLog(_isSourceModAvailable.Value
                ? "[Ops+] SourceMod command path detected."
                : "[Ops+][Info] SourceMod command path unavailable.");
        }

        if (_isSourceModAvailable == true)
        {
            return true;
        }

        AddLog($"[Error] '{command}' needs SourceMod admin plugin support. Command not sent.");
        return false;
    }

    private void CopySelectedHistorySteamId()
    {
        var value = SelectedHistoryPlayer?.SteamId64;
        if (string.IsNullOrWhiteSpace(value))
        {
            value = SelectedHistoryPlayer?.SteamId;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            value = SelectedHistoryPlayer?.Steam3;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            AddLog("[Error] Selected history player does not have a Steam identifier.");
            return;
        }

        Clipboard.SetText(value);
        AddLog($"[Ops+] Copied Steam identifier: {value}");
    }

    private void CopySelectedHistoryIp()
    {
        var ip = PlayerHistoryService.NormalizeIp(SelectedHistoryPlayer?.LastKnownIp ?? string.Empty);
        if (string.IsNullOrWhiteSpace(ip))
        {
            AddLog("[Error] Selected history player does not have an IP.");
            return;
        }

        Clipboard.SetText(ip);
        AddLog($"[Ops+] Copied IP: {ip}");
    }

    private async Task ExportPlayerHistoryAsync()
    {
        var path = string.IsNullOrWhiteSpace(PlayerHistoryExportPath) ? "Data/Exports/player-history.json" : PlayerHistoryExportPath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = JsonSerializer.Serialize(PlayerHistory.ToList(), new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, payload);
        AddLog($"[Ops+] Player history exported: {path}");
    }

    private async Task UnbanSelectedPlayerByIpAsync()
    {
        if (SelectedHistoryPlayer is null)
        {
            AddLog("[Error] Select a historical player first.");
            return;
        }

        var ip = PlayerHistoryService.NormalizeIp(SelectedHistoryPlayer.LastKnownIp);
        if (!System.Net.IPAddress.TryParse(ip, out _))
        {
            AddLog($"[Error] Invalid or unavailable IP for unban: '{SelectedHistoryPlayer.LastKnownIp}'.");
            return;
        }

        await ExecuteAndAuditAsync("PlayerUnbanIp", $"removeip {ip}");
        await ExecuteAndAuditAsync("PlayerUnbanIp", "writeip");
        AddLog("[Ops+][Info] IP unban attempted. Steam-based unban requires server/plugin-specific commands.");
    }

    private async Task RunReadyCheckAsync()
    {
        await ExecuteAndAuditAsync("ReadyCheck", "say [ADMIN] Ready check - type !ready");
    }

    private Task ToggleScheduledAutomationAsync()
    {
        if (_automationCts is not null)
        {
            _automationCts.Cancel();
            _automationCts.Dispose();
            _automationCts = null;
            AddLog("Scheduled automation stopped.");
            OnPropertyChanged(nameof(IsAutomationEnabled));
            return Task.CompletedTask;
        }

        if (!int.TryParse(ScheduleMinutes, out var minutes) || minutes < 1)
        {
            AddLog("[Error] Schedule minutes must be a positive integer.");
            return Task.CompletedTask;
        }

        _automationCts = new CancellationTokenSource();
        AddLog($"Scheduled automation started ({minutes} min, action: {ScheduleAction}).");
        OnPropertyChanged(nameof(IsAutomationEnabled));

        _ = Task.Run(async () =>
        {
            while (_automationCts is not null && !_automationCts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(minutes), _automationCts.Token);
                    await ExecuteScheduledActionAsync(ScheduleAction);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AddLog($"[Error] Scheduled action failed: {ex.Message}");
                }
            }
        });

        return Task.CompletedTask;
    }

    private async Task ExecuteScheduledActionAsync(string action)
    {
        if (action.Contains("map rotation", StringComparison.OrdinalIgnoreCase))
        {
            var nextMap = Maps.FirstOrDefault(m => !m.IsWorkshopMap && !string.IsNullOrWhiteSpace(m.StandardMapName));
            if (nextMap is not null)
            {
                await ExecuteAndAuditAsync("ScheduleMapRotation", $"changelevel {nextMap.StandardMapName}");
            }

            return;
        }

        var preset = PresetPacks.FirstOrDefault(p => p.Name.Equals(action, StringComparison.OrdinalIgnoreCase));
        if (preset is not null)
        {
            foreach (var cmd in preset.Commands.Where(CanExecuteCommandSafely))
            {
                await ExecuteAndAuditAsync($"ScheduledPreset:{preset.Name}", cmd);
            }
        }
    }

    private async Task RunPreMatchChecklistAsync()
    {
        await ExecuteAndAuditAsync("PreMatch", "say [ADMIN] Pre-match checklist started.");
        await ExecuteAndAuditAsync("PreMatch", "mp_warmup_start");
        await ExecuteAndAuditAsync("PreMatch", "sv_pause_on_team_switch 1");
    }

    private async Task RunHalftimeSwitchAsync()
    {
        await ExecuteAndAuditAsync("Halftime", "mp_halftime 1");
        await ExecuteAndAuditAsync("Halftime", "say [ADMIN] Halftime switch procedures in progress.");
    }

    private async Task RunPostMatchArchiveAsync()
    {
        await ExecuteAndAuditAsync("PostMatch", "say [ADMIN] GGWP. Archiving match data.");
        await ExportAuditLogAsync();
    }

    private async Task ExportAuditLogAsync()
    {
        var path = string.IsNullOrWhiteSpace(AuditExportPath) ? "Data/Exports/audit-log.json" : AuditExportPath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = JsonSerializer.Serialize(AuditLogEntries.ToList(), new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, payload);
        AddLog($"Audit log exported: {path}");
    }

    private async Task ClearServerOverridesAsync()
    {
        var resetCommands = new[]
        {
            "exec server.cfg",
            "exec gamemode_competitive.cfg",
            "mp_restartgame 1",
            "say [ADMIN] Server settings reset to default configuration."
        };

        foreach (var command in resetCommands)
        {
            await ExecuteAndAuditAsync("ResetToDefaults", command);
        }

        AddLog("Default server configuration reset sequence completed.");
    }

    private async Task<string> ExecuteAndAuditAsync(string action, string command)
    {
        if (!CanExecuteCommandSafely(command))
        {
            return "[Guardrail] blocked";
        }

        if (DryRunMode)
        {
            var dryRunMessage = $"[DryRun] {command}";
            AddLog(dryRunMessage);
            AuditLogEntries.Add(new AuditLogEntry { Action = action, CommandText = command, ResponsePreview = dryRunMessage });
            return dryRunMessage;
        }

        var response = await _rconService.SendCommandAsync(command);
        AddLog(response);
        AuditLogEntries.Add(new AuditLogEntry
        {
            Action = action,
            CommandText = command,
            ResponsePreview = response.Length > 120 ? response[..120] : response
        });
        return response;
    }

    private bool CanExecuteCommandSafely(string command)
    {
        var normalized = command.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var denylist = CommandDenylist.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (denylist.Any(blocked => normalized.Contains(blocked, StringComparison.OrdinalIgnoreCase)))
        {
            AddLog($"[Guardrail] Denied command: {normalized}");
            return false;
        }

        var allowlist = CommandAllowlist.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (allowlist.Length > 0 && !allowlist.Any(allowed => normalized.Contains(allowed, StringComparison.OrdinalIgnoreCase)))
        {
            AddLog($"[Guardrail] Not in allowlist: {normalized}");
            return false;
        }

        if (RequireDestructiveConfirm && (normalized.Contains("kick", StringComparison.OrdinalIgnoreCase) || normalized.Contains("ban", StringComparison.OrdinalIgnoreCase)))
        {
            var confirmed = MessageBox.Show($"Confirm destructive command?\n{normalized}", "Confirm command", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return confirmed == MessageBoxResult.Yes;
        }

        return true;
    }

    

    private static List<PresetCommandPack> BuildDefaultPresetPacks()
    {
        return
        [
            new PresetCommandPack { Name = "Warmup Pack", Description = "Warmup utilities", Commands = ["mp_warmup_start", "mp_freezetime 5"], RollbackCommands = ["mp_warmup_end"] },
            new PresetCommandPack { Name = "Knife Round", Description = "Knife-round setup", Commands = ["mp_roundtime 2", "mp_restartgame 1"], RollbackCommands = ["mp_roundtime 1.92"] },
            new PresetCommandPack { Name = "Overtime Pack", Description = "Overtime config", Commands = ["mp_overtime_enable 1", "mp_overtime_maxrounds 6"], RollbackCommands = ["mp_overtime_enable 0"] },
            new PresetCommandPack { Name = "Practice Mode", Description = "Practice utilities", Commands = ["sv_cheats 1", "mp_limitteams 0"], RollbackCommands = ["sv_cheats 0"] }
        ];
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

    private bool FilterPlayerHistory(object obj)
    {
        if (obj is not PlayerHistoryEntry entry)
        {
            return false;
        }

        var q = PlayerHistorySearchText.Trim();
        return string.IsNullOrWhiteSpace(q)
               || entry.LastKnownName.Contains(q, StringComparison.OrdinalIgnoreCase)
               || entry.SteamId.Contains(q, StringComparison.OrdinalIgnoreCase)
               || entry.SteamId64.Contains(q, StringComparison.OrdinalIgnoreCase)
               || entry.Steam3.Contains(q, StringComparison.OrdinalIgnoreCase)
               || entry.LastKnownIp.Contains(q, StringComparison.OrdinalIgnoreCase);
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
        OnPropertyChanged(nameof(TickRateVariance));
        OnPropertyChanged(nameof(ChokePercent));
        OnPropertyChanged(nameof(LossPercent));
        OnPropertyChanged(nameof(BotCount));
        OnPropertyChanged(nameof(HealthAlert));
    }

    private static ServerHealthSnapshot BuildHealthSnapshot(string stats, string status)
    {
        var tickValues = Regex.Matches(stats, @"tick(?:rate)?\s*[:=]\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)
            .Select(m => double.TryParse(m.Groups[1].Value, out var v) ? v : 0)
            .Where(v => v > 0)
            .ToList();
        var meanTick = tickValues.Count == 0 ? 0 : tickValues.Average();
        var variance = tickValues.Count < 2 ? 0 : tickValues.Average(v => Math.Pow(v - meanTick, 2));

        static double ParsePercent(string input, string key)
        {
            var match = Regex.Match(input, $"{key}\\s*[:=]\\s*(\\d+(?:\\.\\d+)?)", RegexOptions.IgnoreCase);
            return match.Success && double.TryParse(match.Groups[1].Value, out var val) ? val : 0;
        }

        var choke = ParsePercent(stats, "choke");
        var loss = ParsePercent(stats, "loss");
        var botsMatch = Regex.Match(status, @"players\s*:\s*\d+\s+humans,\s*(\d+)\s+bots", RegexOptions.IgnoreCase);
        var bots = botsMatch.Success && int.TryParse(botsMatch.Groups[1].Value, out var parsedBots) ? parsedBots : 0;

        var alert = (choke > 10 || loss > 5 || variance > 3)
            ? "Warning: network/perf instability"
            : "Healthy";

        return new ServerHealthSnapshot
        {
            TickRateVariance = Math.Round(variance, 2),
            ChokePercent = Math.Round(choke, 2),
            LossPercent = Math.Round(loss, 2),
            BotCount = bots,
            Alert = alert
        };
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
        (DeleteServerProfileCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
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
        (RunPresetPackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RollbackPresetPackCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RefreshPlayersCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (KickPlayerCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (BanPlayerCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (MutePlayerCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (SwapPlayerTeamCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CopyPlayerSteamIdCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CopyPlayerIpCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportPlayerHistoryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (UnbanPlayerByIpCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ReadyCheckCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ToggleAutomationCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RunPreMatchChecklistCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RunHalftimeSwitchCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RunPostMatchArchiveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExportAuditLogCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ClearServerOverridesCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ResortSavedServers()
    {
        var ordered = SavedServers.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        ApplyCollection(SavedServers, ordered);
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
