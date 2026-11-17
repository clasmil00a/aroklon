using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

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

    private void OnReportSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is ReportsViewModel vm)
        {
            vm.GenerateReportCommand.Execute(null);
        }
    }
}