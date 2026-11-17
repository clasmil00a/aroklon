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

    private void OnDialogTextBoxVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && sender is TextBox box)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    box.Focus();
                    box.CaretIndex = box.Text.Length;
                }));
        }
    }

    private void OnCartRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is not PosViewModel vm)
        {
            return;
        }

        var item = ItemsControl.ContainerFromElement(
            CartList,
            e.OriginalSource as DependencyObject) as ListBoxItem;
        if (item?.DataContext is CartLineViewModel line)
        {
            vm.ShowPriceOverrideCommand.Execute(line);
        }
    }

    /// <summary>On-screen keypad: appends the button content to the tagged text box.</summary>
    private void OnKeypadDigitClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string targetName } button)
        {
            return;
        }

        if (FindName(targetName) is not TextBox box)
        {
            return;
        }

        var key = button.Content?.ToString() ?? string.Empty;
        switch (key)
        {
            case "\u232B":
                if (box.Text.Length > 0)
                {
                    box.Text = box.Text[..^1];
                }
                break;
            case ".":
                if (!box.Text.Contains('.'))
                {
                    box.Text += ".";
                }
                break;
            default:
                box.Text += key;
                break;
        }

        box.CaretIndex = box.Text.Length;
        box.Focus();
    }

    private void OnPaymentOverlayVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    PaymentAmountBox.Focus();
                    PaymentAmountBox.CaretIndex = PaymentAmountBox.Text.Length;
                }));
        }
    }

    private void OnFocusPaymentAmountClick(object sender, RoutedEventArgs e)
    {
        PaymentKeypad.Visibility = Visibility.Visible;
        PaymentAmountBox.Focus();
        PaymentAmountBox.CaretIndex = PaymentAmountBox.Text.Length;
    }

    private void OnTogglePaymentKeypadClick(object sender, RoutedEventArgs e)
    {
        PaymentKeypad.Visibility = PaymentKeypad.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (PaymentKeypad.Visibility == Visibility.Visible)
        {
            PaymentAmountBox.Focus();
            PaymentAmountBox.CaretIndex = PaymentAmountBox.Text.Length;
        }
    }
}
