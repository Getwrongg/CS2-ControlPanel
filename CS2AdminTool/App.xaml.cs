using System.Windows;
using System.Windows.Threading;
using CS2AdminTool.Services;
using CS2AdminTool.ViewModels;

namespace CS2AdminTool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;

        try
        {
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
        catch (Exception ex)
        {
            MessageBox.Show($"Startup failed: {ex.Message}", "CS2AdminTool", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Unexpected UI error: {e.Exception.Message}", "CS2AdminTool", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show($"Fatal error: {ex.Message}", "CS2AdminTool", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        MessageBox.Show($"Background task error: {e.Exception.Message}", "CS2AdminTool", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.SetObserved();
    }
}
