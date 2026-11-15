using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class EndOfDayView : UserControl
{
    public EndOfDayView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is EndOfDayViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
