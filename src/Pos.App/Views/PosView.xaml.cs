using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class PosView : UserControl
{
    public PosView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PosViewModel)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() => ProductSearchBox.Focus()));
        }
    }

    private void OnCustomerDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is PosViewModel vm &&
            sender is ListBoxItem { DataContext: Pos.Domain.Models.CustomerSummary customer })
        {
            vm.ApplyCustomerCommand.Execute(customer);
        }
    }

    private void OnBarcodeBoxVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() => BarcodeBox.Focus()));
        }
    }
}
