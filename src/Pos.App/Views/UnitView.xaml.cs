using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class UnitView : UserControl
{
    public UnitView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is UnitViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
