using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class PaymentTypeView : UserControl
{
    public PaymentTypeView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is PaymentTypeViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
