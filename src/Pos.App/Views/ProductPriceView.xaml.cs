using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class ProductPriceView : UserControl
{
    public ProductPriceView()
    {
        InitializeComponent();
    }

    public string? ProductId { get; set; }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ProductPriceViewModel vm && ProductId is not null)
        {
            vm.SetProduct(ProductId);
            _ = vm.InitializeAsync();
        }
    }
}
