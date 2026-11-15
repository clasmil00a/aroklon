using System.Windows.Controls;
using Aroklon.App.ViewModels;

namespace Aroklon.App.Views;

public partial class DatabaseView : UserControl
{
    public DatabaseView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is DatabaseViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
