using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class PriceListView : UserControl
{
    public PriceListView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is PriceListViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
