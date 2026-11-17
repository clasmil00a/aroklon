using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class StockAdjustmentViewModel : ObservableObject
{
    private readonly ProductService _products;
    private readonly StockAdjustmentService _stock;

    [ObservableProperty]
    private ObservableCollection<ProductStockRow> _productList = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _selectedProductId;

    [ObservableProperty]
    private string _selectedProductName = string.Empty;

    [ObservableProperty]
    private long _currentStock;

    [ObservableProperty]
    private long _targetStock;

    [ObservableProperty]
    private string _reason = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public StockAdjustmentViewModel(ProductService products, StockAdjustmentService stock)
    {
        _products = products;
        _stock = stock;
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var list = await _products.SearchAsync(SearchText, 100);
            ProductList = new ObservableCollection<ProductStockRow>(
                list.Select(p => new ProductStockRow(
                    p.ProductId,
                    p.Name,
                    p.Code ?? string.Empty,
                    0, // stock loaded on selection
                    p.UnitName ?? "pcs")));
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load products for stock adjustment");
            ErrorMessage = "Gagal memuat daftar produk. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectProduct(ProductStockRow? product)
    {
        if (product is null) return;

        SelectedProductId = product.ProductId;
        SelectedProductName = product.ProductName;

        try
        {
            var stock = await _stock.GetOnHandAsync(product.ProductId);
            CurrentStock = stock;
            TargetStock = stock;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load stock for {ProductId}", product.ProductId);
            ErrorMessage = "Gagal memuat stok produk.";
        }
    }

    [RelayCommand]
    private async Task AdjustAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(SelectedProductId)) return;
        if (string.IsNullOrWhiteSpace(Reason))
        {
            ErrorMessage = "Alasan penyesuaian wajib diisi.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        try
        {
            await _stock.AdjustStockAsync(SelectedProductId, TargetStock, Reason.Trim());

            InfoMessage = "Stok berhasil disesuaikan.";
            Reason = string.Empty;

            // Refresh stock
            var stock = await _stock.GetOnHandAsync(SelectedProductId);
            CurrentStock = stock;
            TargetStock = stock;

            await LoadAsync();
        }
        catch (ValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to adjust stock");
            ErrorMessage = "Gagal menyesuaikan stok. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ReceiveAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(SelectedProductId)) return;
        if (TargetStock <= 0)
        {
            ErrorMessage = "Jumlah stok masuk harus lebih dari nol.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Reason))
        {
            ErrorMessage = "Alasan stok masuk wajib diisi.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        try
        {
            await _stock.ReceiveStockAsync(SelectedProductId, TargetStock, Reason.Trim());

            InfoMessage = "Stok masuk berhasil dicatat.";
            Reason = string.Empty;
            TargetStock = 0;

            var stock = await _stock.GetOnHandAsync(SelectedProductId);
            CurrentStock = stock;

            await LoadAsync();
        }
        catch (ValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to receive stock");
            ErrorMessage = "Gagal mencatat stok masuk. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public event Action? BackRequested;
}

public sealed record ProductStockRow(
    string ProductId,
    string ProductName,
    string Code,
    long StockOnHand,
    string UnitName);
