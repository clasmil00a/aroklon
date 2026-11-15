using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class SalesHistoryView : UserControl
{
    public SalesHistoryView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SalesHistoryViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
