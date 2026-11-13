using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class CustomerSelectionView : UserControl
{
    public CustomerSelectionView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is CustomerSelectionViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
