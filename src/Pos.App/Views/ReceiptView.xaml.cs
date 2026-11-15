using System.Windows.Controls;
using Aroklon.App.ViewModels;

namespace Aroklon.App.Views;

public partial class ReceiptView : UserControl
{
    public ReceiptView()
    {
        InitializeComponent();
    }

    public string? SaleId { get; set; }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ReceiptViewModel vm && SaleId is not null)
        {
            vm.SetSale(SaleId);
            _ = vm.InitializeAsync();
        }
    }
}
