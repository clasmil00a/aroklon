using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class OpenOrdersView : UserControl
{
    public OpenOrdersView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is OpenOrdersViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
