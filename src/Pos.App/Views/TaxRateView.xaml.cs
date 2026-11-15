using System.Windows.Controls;
using Aroklon.App.ViewModels;

namespace Aroklon.App.Views;

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
