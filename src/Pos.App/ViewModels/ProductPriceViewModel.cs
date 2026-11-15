using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aroklon.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Aroklon.App.ViewModels;

public sealed record PriceViewLine(
    string PriceListName,
    string UnitName,
    string? UnitSymbol,
    long? PriceMinor,
    bool IsInherited);

public sealed partial class ProductPriceViewModel : ObservableObject
{
    private readonly ProductService _products;
    private readonly PriceListService _priceLists;

    [ObservableProperty]
    private ProductDetail? _detail;

    [ObservableProperty]
    private ObservableCollection<PriceViewLine> _lines = new();

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    private string _productId = string.Empty;

    public ProductPriceViewModel(ProductService products, PriceListService priceLists)
    {
        _products = products;
        _priceLists = priceLists;
    }

    public void SetProduct(string productId) => _productId = productId;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public async Task InitializeAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(_productId))
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            Detail = await _products.GetProductDetailAsync(_productId);

            var lists = await _priceLists.ListAsync();
            var lines = new List<PriceViewLine>();
            foreach (var list in lists)
            {
                var items = await _priceLists.GetItemsAsync(list.Id);
                foreach (var variant in Detail.Variants.Where(v => v.Active))
                {
                    var item = items.FirstOrDefault(i => i.ProductUnitId == variant.ProductUnitId);
                    lines.Add(new PriceViewLine(
                        list.Name,
                        variant.UnitName,
                        variant.UnitSymbol,
                        item?.PriceMinor,
                        item?.IsInherited ?? true));
                }
            }

            Lines = new ObservableCollection<PriceViewLine>(lines);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load product prices");
            ErrorMessage = "Gagal memuat harga produk. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public event Action? BackRequested;
}
