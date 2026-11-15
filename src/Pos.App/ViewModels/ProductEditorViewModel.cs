using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aroklon.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Aroklon.App.ViewModels;

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

    // Variant editing
    [ObservableProperty]
    private string? _editingVariantId;

    [ObservableProperty]
    private long _editingFactor;

    [ObservableProperty]
    private long? _editingPrice;

    [ObservableProperty]
    private bool _editingIsDefaultSale;

    [ObservableProperty]
    private bool _editingActive;

    [ObservableProperty]
    private string _editingError = string.Empty;

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
    private void EditVariant(ProductUnitVariant? variant)
    {
        if (variant is null || IsBusy) return;

        EditingVariantId = variant.ProductUnitId;
        EditingFactor = variant.FactorToBaseMilli;
        EditingPrice = variant.PriceMinor;
        EditingIsDefaultSale = variant.IsDefaultSale;
        EditingActive = variant.Active;
        EditingError = string.Empty;
    }

    [RelayCommand]
    private void CancelEditVariant()
    {
        EditingVariantId = null;
        EditingFactor = 1000;
        EditingPrice = null;
        EditingIsDefaultSale = false;
        EditingActive = true;
        EditingError = string.Empty;
    }

    [RelayCommand]
    private async Task UpdateVariantAsync()
    {
        if (EditingVariantId is null || IsBusy) return;

        if (EditingFactor <= 0)
        {
            EditingError = "Faktor satuan harus lebih dari nol.";
            return;
        }

        if (EditingPrice is not null && EditingPrice.Value < 0)
        {
            EditingError = "Harga tidak boleh negatif.";
            return;
        }

        IsBusy = true;
        EditingError = string.Empty;
        ErrorMessage = string.Empty;

        try
        {
            await _products.UpdateProductUnitAsync(EditingVariantId, new(
                EditingFactor,
                EditingIsDefaultSale,
                EditingPrice,
                null, // sortOrder - keep existing
                EditingActive
            ));

            InfoMessage = "Varian satuan diperbarui.";
            CancelEditVariant();
            await InitializeAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to update variant");
            EditingError = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteVariantAsync(ProductUnitVariant? variant)
    {
        if (variant is null || IsBusy) return;

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await _products.DeleteProductUnitAsync(variant.ProductUnitId);
            InfoMessage = "Varian satuan dihapus.";
            await InitializeAsync();
        }
        catch (Pos.Domain.ConflictException)
        {
            ErrorMessage = "Tidak dapat menghapus varian karena sudah digunakan dalam transaksi.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to delete variant");
            ErrorMessage = "Gagal menghapus varian satuan. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleVariantActiveAsync(ProductUnitVariant? variant)
    {
        if (variant is null || IsBusy) return;

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await _products.UpdateProductUnitAsync(variant.ProductUnitId, new(
                variant.FactorToBaseMilli,
                variant.IsDefaultSale,
                variant.PriceMinor,
                null, // sortOrder - keep existing
                !variant.Active
            ));

            InfoMessage = variant.Active ? "Varian dinonaktifkan." : "Varian diaktifkan.";
            await InitializeAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to toggle variant");
            ErrorMessage = "Gagal mengubah status varian. Coba lagi.";
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
