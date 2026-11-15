using System.Windows.Controls;
using Aroklon.App.ViewModels;

namespace Aroklon.App.Views;

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
