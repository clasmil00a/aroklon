using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class ProductListViewModel : ObservableObject
{
    private readonly ProductService _products;

    [ObservableProperty]
    private ObservableCollection<ProductLookup> _productList = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ProductLookup? _selectedProduct;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public event Action<string>? ProductSelected;
    public event Action? NewProductRequested;
    public event Action<string>? NavigateRequested;
    public event Action? BackRequested;

    public ProductListViewModel(ProductService products)
    {
        _products = products;
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;
    public bool HasSelection => SelectedProduct is not null;
    public bool HasProducts => ProductList.Count > 0;
    public bool HasNoProducts => !HasProducts;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));
    partial void OnSelectedProductChanged(ProductLookup? value) =>
        OnPropertyChanged(nameof(HasSelection));
    partial void OnSearchTextChanged(string value) => _ = LoadAsync();
    partial void OnProductListChanged(ObservableCollection<ProductLookup> value)
    {
        OnPropertyChanged(nameof(HasProducts));
        OnPropertyChanged(nameof(HasNoProducts));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var list = await _products.SearchAsync(SearchText, 50);
            ProductList = new ObservableCollection<ProductLookup>(list);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load products");
            ErrorMessage = "Gagal memuat daftar produk. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task InitializeAsync() => LoadAsync();

    [RelayCommand]
    private void SelectProduct(ProductLookup? product)
    {
        if (product is not null)
        {
            ProductSelected?.Invoke(product.ProductId);
        }
    }

    [RelayCommand]
    private void EditSelectedProduct()
    {
        if (SelectedProduct is not null)
        {
            ProductSelected?.Invoke(SelectedProduct.ProductId);
        }
    }

    [RelayCommand]
    private void NewProduct() => NewProductRequested?.Invoke();

    [RelayCommand]
    private void OpenProductGroups() => NavigateRequested?.Invoke("S43");

    [RelayCommand]
    private void OpenReports() => NavigateRequested?.Invoke("S44");

    [RelayCommand]
    private void OpenHistory() => NavigateRequested?.Invoke("S32");

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadAsync();
    }
}
