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
        var commandService = new CommandService(rconService);
        var viewModel = new MainViewModel(commandService, rconService);

        var window = new MainWindow
        {
            DataContext = viewModel
        };

        window.Show();
    }
}
