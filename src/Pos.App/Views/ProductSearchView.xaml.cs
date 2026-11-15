using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class ProductSearchView : UserControl
{
    public ProductSearchView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProductSearchViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ProductSearchViewModel vm) return;

        switch (e.Key)
        {
            case Key.Up:
                vm.MoveUpCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Down:
                vm.MoveDownCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter:
                vm.SelectCurrentCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                vm.CloseCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
