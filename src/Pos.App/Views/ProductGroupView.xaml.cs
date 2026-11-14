using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class ProductGroupView : UserControl
{
    public ProductGroupView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ProductGroupViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
