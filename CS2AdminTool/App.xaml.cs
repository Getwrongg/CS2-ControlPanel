using System.Windows;
using CS2AdminTool.Services;
using CS2AdminTool.ViewModels;

namespace CS2AdminTool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var rconService = new RconService();
        var jsonStorage = new JsonStorageService();
        var configLibraryService = new ConfigLibraryService(jsonStorage);
        var mapLibraryService = new MapLibraryService();
        var executionService = new CommandExecutionService(rconService);
        var configRunnerService = new ConfigRunnerService(executionService);

        var serverMonitorService = new ServerMonitorService(rconService);

        var viewModel = new MainViewModel(configLibraryService, mapLibraryService, configRunnerService, rconService, serverMonitorService);

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
    }
}
