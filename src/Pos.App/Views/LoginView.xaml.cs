using System.Windows;
using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel &&
            !string.IsNullOrWhiteSpace(viewModel.Username))
        {
            PasswordBox.Focus();
        }
        else
        {
            UsernameBox.Focus();
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.Password = PasswordBox.Password;
        }
    }
}
