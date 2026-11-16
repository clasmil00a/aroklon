using System;
using System.Windows;
using System.Windows.Input;

namespace Pos.App;

public partial class MainWindow : Window
{
    private const double NormalWidth = 1366;
    private const double NormalHeight = 768;

    private bool _isFullScreen = true;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Applies the per-user startup preference before the window is shown.
    /// </summary>
    public void ApplyStartupMode(bool fullScreen)
    {
        if (fullScreen)
        {
            EnterFullScreen();
        }
        else
        {
            LeaveFullScreen();
        }
    }

    public void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            LeaveFullScreen();
        }
        else
        {
            EnterFullScreen();
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private void EnterFullScreen()
    {
        // Reset first so a window that was restored with a title bar gets a
        // clean borderless maximize on the current monitor.
        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        _isFullScreen = true;
    }

    private void LeaveFullScreen()
    {
        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;

        var workArea = SystemParameters.WorkArea;
        var width = Math.Min(NormalWidth, workArea.Width);
        var height = Math.Min(NormalHeight, workArea.Height);
        Width = width;
        Height = height;
        Left = workArea.Left + ((workArea.Width - width) / 2);
        Top = workArea.Top + ((workArea.Height - height) / 2);

        _isFullScreen = false;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isFullScreen)
        {
            // Re-assert after the window handle exists; some Windows versions
            // drop WindowStyle=None while creating a maximized window.
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
    }
}
