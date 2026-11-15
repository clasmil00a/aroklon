using System.Windows.Controls;
using Aroklon.App.ViewModels;

namespace Aroklon.App.Views;

public partial class StockAdjustmentView : UserControl
{
    public StockAdjustmentView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is StockAdjustmentViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
