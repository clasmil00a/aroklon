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

    private void OnProductDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is ProductListViewModel vm)
        {
            vm.EditSelectedProductCommand.Execute(null);
        }
    }
}
