using System.IO;
using System.Windows;
using Edc15EepromTool.Services;
using Edc15EepromTool.ViewModels;

namespace Edc15EepromTool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

#if DEBUG
        // developer aid: "--render <out.png> <scenario> [dump.bin]" draws the window to a PNG and exits
        if (e.Args.Length >= 3 && e.Args[0] == "--render")
        {
            ScreenshotRenderer.Render(e.Args[1], e.Args[2], e.Args.Length > 3 ? e.Args[3] : null);
            return;
        }
#endif

        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            MessageBox.Show($"Something went wrong and the program has to close.\n\n{args.Exception.Message}",
                "Bosch EDC15 EEPROM Tool", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        };

        ThemeManager.FollowSystem(this);

        var viewModel = new MainViewModel(new ShellService());
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();

        // a dump passed on the command line, for example by "Open with"
        if (e.Args.Length == 1 && File.Exists(e.Args[0]))
        {
            viewModel.OpenDropped(e.Args[0]);
        }
    }
}
