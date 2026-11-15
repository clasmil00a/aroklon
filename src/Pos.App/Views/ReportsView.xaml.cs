using System.Windows.Controls;
using Aroklon.App.ViewModels;

namespace Aroklon.App.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ReportsViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}