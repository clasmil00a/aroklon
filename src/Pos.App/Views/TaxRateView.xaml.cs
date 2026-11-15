using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class TaxRateView : UserControl
{
    public TaxRateView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is TaxRateViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
