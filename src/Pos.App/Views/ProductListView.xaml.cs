using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class ProductListView : UserControl
{
    public ProductListView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ProductListViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
