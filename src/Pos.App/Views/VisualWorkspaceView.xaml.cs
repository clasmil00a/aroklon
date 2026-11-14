using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class VisualWorkspaceView : UserControl
{
    public VisualWorkspaceView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is VisualWorkspaceViewModel vm)
        {
            _ = vm.LoadAsync();
        }
    }
}
