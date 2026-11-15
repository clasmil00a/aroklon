using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

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
