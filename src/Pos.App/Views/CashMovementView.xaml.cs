using System.Windows.Controls;
using Aroklon.App.ViewModels;

namespace Aroklon.App.Views;

public partial class CashMovementView : UserControl
{
    public CashMovementView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is CashMovementViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
