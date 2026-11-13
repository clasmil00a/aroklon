using System.Windows.Controls;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class ProductEditorView : UserControl
{
    public ProductEditorView()
    {
        InitializeComponent();
    }

    public string? ProductId { get; set; }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ProductEditorViewModel vm && ProductId is not null)
        {
            vm.SetProduct(ProductId);
            _ = vm.InitializeAsync();
        }
    }
}
