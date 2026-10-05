using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Edc15EepromTool.ViewModels;

namespace Edc15EepromTool;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _closeConfirmed;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        SourceInitialized += (_, _) => UseRoundedCorners();
        DragEnter += OnDragEnter;
        DragOver += OnDragOver;
        DragLeave += OnDragLeave;
        Drop += OnDrop;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_closeConfirmed && !_viewModel.RequestClose(ConfirmedClose))
        {
            e.Cancel = true;
        }
        base.OnClosing(e);
    }

    private void ConfirmedClose()
    {
        _closeConfirmed = true;
        Close();
    }

    // ---------------------------------------------------------------- title bar

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ---------------------------------------------------------------- drag and drop

    private static string? DroppedFile(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files ? files[0] : null;

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (DroppedFile(e) is not null)
        {
            DropHint.Visibility = Visibility.Visible;
        }
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        var position = e.GetPosition(this);
        if (position.X <= 0 || position.Y <= 0 || position.X >= ActualWidth || position.Y >= ActualHeight)
        {
            DropHint.Visibility = Visibility.Collapsed;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DroppedFile(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropHint.Visibility = Visibility.Collapsed;
        if (DroppedFile(e) is { } path)
        {
            Activate();
            _viewModel.OpenDropped(path);
        }
    }

    // ---------------------------------------------------------------- Windows 11 rounded corners

    private void UseRoundedCorners()
    {
        const int DwmwaWindowCornerPreference = 33;
        const int DwmwcpRound = 2;

        var handle = new WindowInteropHelper(this).Handle;
        var preference = DwmwcpRound;
        // fails quietly on Windows 10, which has no rounded window corners
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
