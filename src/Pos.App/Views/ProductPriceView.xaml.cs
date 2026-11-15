using System.Windows.Controls;
using Aroklon.App.ViewModels;

namespace Aroklon.App.Views;

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
