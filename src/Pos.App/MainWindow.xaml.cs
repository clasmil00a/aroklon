using System;
using System.Windows;
using System.Windows.Input;

namespace Pos.App;

public partial class MainWindow : Window
{
    private const double NormalWidth = 1366;
    private const double NormalHeight = 768;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Applies the per-user startup preference before the window is shown.
    /// A maximized window keeps the Windows title bar and taskbar visible,
    /// unlike a borderless full-screen window.
    /// </summary>
    public void ApplyStartupMode(bool maximized)
    {
        if (maximized)
        {
            Maximize();
        }
        else
        {
            RestoreWindow();
        }
    }

    public void ToggleMaximized()
    {
        if (WindowState == WindowState.Maximized)
        {
            RestoreWindow();
        }
        else
        {
            Maximize();
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleMaximized();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private void Maximize()
    {
        WindowState = WindowState.Maximized;
    }

    private void RestoreWindow()
    {
        WindowState = WindowState.Normal;

        var workArea = SystemParameters.WorkArea;
        var width = Math.Min(NormalWidth, workArea.Width);
        var height = Math.Min(NormalHeight, workArea.Height);
        Width = width;
        Height = height;
        Left = workArea.Left + ((workArea.Width - width) / 2);
        Top = workArea.Top + ((workArea.Height - height) / 2);
    }
}
