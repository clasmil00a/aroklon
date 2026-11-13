using System.Windows;
using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class OnboardingView : UserControl
{
    public OnboardingView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ShopNameBox.Focus();
        ShopNameBox.SelectAll();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is OnboardingViewModel viewModel)
        {
            viewModel.Password = PasswordBox.Password;
        }
    }

    private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is OnboardingViewModel viewModel)
        {
            viewModel.ConfirmPassword = ConfirmBox.Password;
        }
    }
}
