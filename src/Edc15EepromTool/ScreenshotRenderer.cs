#if DEBUG
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Edc15EepromTool.Services;
using Edc15EepromTool.ViewModels;

namespace Edc15EepromTool;

/// <summary>
/// Developer aid for documentation and visual checks: draws the main window in a given state to a PNG file
/// without showing it on screen. Only compiled into debug builds.
/// </summary>
internal static class ScreenshotRenderer
{
    private sealed class NoShell : IShellService
    {
        public string? PickFileToOpen() => null;
        public string? PickFileToSave(string suggestedName, string? folder) => null;
        public void OpenUrl(string url) { }
        public bool CopyText(string text) => true;
    }

    public static void Render(string output, string scenario, string? dump)
    {
        var light = scenario.EndsWith(":light", StringComparison.Ordinal);
        scenario = scenario.Split(':')[0];
        ThemeManager.Apply(light);

        var viewModel = new MainViewModel(new NoShell());
        var window = new MainWindow(viewModel)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            ShowInTaskbar = false,
            ShowActivated = false,
        };
        Application.Current.MainWindow = window;
        window.Show();

        if (dump is not null)
        {
            viewModel.Load(dump);
        }

        switch (scenario)
        {
            case "edited":
                viewModel.DismissToastCommand.Execute(null);
                viewModel.IsImmoOn = !viewModel.IsImmoOn;
                viewModel.MileageText = "150000";
                break;
            case "invalid":
                viewModel.DismissToastCommand.Execute(null);
                viewModel.MileageText = string.Empty;
                viewModel.LoginCodeText = "123";
                break;
            case "about":
                viewModel.DismissToastCommand.Execute(null);
                viewModel.AboutCommand.Execute(null);
                break;
            case "confirm":
                viewModel.MileageText = "150000";
                window.Close();   // asks before discarding the change
                break;
            case "notoast":
                viewModel.DismissToastCommand.Execute(null);
                break;

        }

        // let short animations such as the slide of the immobilizer switch finish before the window is drawn
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);

        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

        var root = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle((Brush)Application.Current.Resources["BgBrush"], null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
            context.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
        }
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(output))
        {
            encoder.Save(stream);
        }

        Application.Current.Shutdown();
    }
}
#endif
