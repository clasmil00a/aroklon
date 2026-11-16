using System.Windows.Controls;
using System.Windows.Input;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();
    }

    private void OnMenuBackdropMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ShellViewModel viewModel)
        {
            viewModel.IsMenuOpen = false;
        }
    }
}
