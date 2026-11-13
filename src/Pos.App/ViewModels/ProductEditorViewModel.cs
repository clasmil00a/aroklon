using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class ProductEditorViewModel : ObservableObject
{
    private readonly ProductService _products;

    [ObservableProperty]
    private ProductDetail? _detail;

    [ObservableProperty]
    private string _newUnitName = string.Empty;

    [ObservableProperty]
    private string? _newUnitSymbol;

    [ObservableProperty]
    private long _newFactor = 1000;

    [ObservableProperty]
    private bool _newIsDefaultSale;

    [ObservableProperty]
    private long? _newPrice;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    private string _productId = string.Empty;

    public ProductEditorViewModel(ProductService products)
    {
        _products = products;
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
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load product detail");
            ErrorMessage = "Gagal memuat detail produk. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddVariantAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        if (Detail is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(NewUnitName))
        {
            ErrorMessage = "Satuan wajib dipilih.";
            return;
        }

        if (NewFactor <= 0)
        {
            ErrorMessage = "Faktor satuan harus lebih dari nol.";
            return;
        }

        if (NewPrice is not null && NewPrice.Value < 0)
        {
            ErrorMessage = "Harga tidak boleh negatif.";
            return;
        }

        IsBusy = true;
        try
        {
            var unit = Detail.Variants.FirstOrDefault(v =>
                v.UnitName.Equals(NewUnitName, StringComparison.OrdinalIgnoreCase))?.UnitId;
            if (unit is null)
            {
                ErrorMessage = "Satuan tidak tersedia. Tambahkan dulu di Master Satuan.";
                return;
            }

            await _products.CreateProductUnitAsync(new(
                _productId, unit, NewFactor, NewIsDefaultSale, NewPrice, 0));

            InfoMessage = "Varian satuan ditambahkan.";
            NewUnitName = string.Empty;
            NewUnitSymbol = null;
            NewFactor = 1000;
            NewIsDefaultSale = false;
            NewPrice = null;
            await InitializeAsync();
        }
        catch (Pos.Domain.ConflictException)
        {
            ErrorMessage = "Varian satuan sudah ada untuk produk ini.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to add variant");
            ErrorMessage = "Gagal menambahkan varian satuan. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public event Action? BackRequested;
    public event Action<string>? PriceViewRequested;

    [RelayCommand]
    private void ViewPrices() => PriceViewRequested?.Invoke(_productId);
}
