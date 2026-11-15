using System.Windows.Controls;
using Aroklon.App.ViewModels;

namespace Aroklon.App.Views;

public partial class PriceListEditorView : UserControl
{
    public PriceListEditorView()
    {
        InitializeComponent();
    }

    public string? PriceListId { get; set; }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is PriceListEditorViewModel vm && PriceListId is not null)
        {
            vm.SetPriceList(PriceListId);
            _ = vm.InitializeAsync();
        }
    }
}
