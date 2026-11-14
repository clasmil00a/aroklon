using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class UserManagementView : UserControl
{
    public UserManagementView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
