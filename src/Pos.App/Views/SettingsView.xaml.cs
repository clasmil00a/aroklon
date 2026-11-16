using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Pos.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Wheel handling for pages whose ScrollViewer is nested inside the
    /// selected TabItem. Preview is used so a wheel gesture always reaches the
    /// page even if an inner control marks the bubbling event as handled.
    /// Text boxes keep their own scrolling.
    /// </summary>
    private void OnTabControlPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not TabControl tabControl)
        {
            return;
        }

        if (FindAncestor<TextBoxBase>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        var scrollViewer = FindVisualChild<ScrollViewer>(tabControl);
        if (scrollViewer is null || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(
            scrollViewer.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? child)
        where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match)
            {
                return match;
            }

            child = child is Visual or Visual3D
                ? VisualTreeHelper.GetParent(child)
                : LogicalTreeHelper.GetParent(child);
        }

        return null;
    }
}
