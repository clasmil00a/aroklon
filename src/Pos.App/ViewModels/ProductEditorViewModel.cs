using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class ProductEditorViewModel : ObservableObject
{
    private readonly ProductService _products;
    private readonly UnitService _units;
    private readonly ProductGroupService _productGroups;

    [ObservableProperty]
    private ProductDetail? _detail;

    // Core product fields (create + edit)
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _code;

    [ObservableProperty]
    private long _priceMinor;

    [ObservableProperty]
    private long _costMinor;

    [ObservableProperty]
    private long _openingStock;

    [ObservableProperty]
    private string? _barcode;

    [ObservableProperty]
    private bool _isService;

    [ObservableProperty]
    private bool _priceChangeAllowed = true;

    [ObservableProperty]
    private ObservableCollection<UnitOption> _unitOptions = new();

    [ObservableProperty]
    private UnitOption? _selectedBaseUnit;

    [ObservableProperty]
    private ObservableCollection<ProductGroup> _groupOptions = new();

    [ObservableProperty]
    private ProductGroup? _selectedGroup;

    [ObservableProperty]
    private bool _isNewProduct = true;

    [ObservableProperty]
    private UnitOption? _newUnitOption;

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

    public ProductEditorViewModel(
        ProductService products,
        UnitService units,
        ProductGroupService productGroups)
    {
        _products = products;
        _units = units;
        _productGroups = productGroups;
    }

    public void SetProduct(string productId)
    {
        _productId = productId;
        IsNewProduct = string.IsNullOrWhiteSpace(productId);
    }

    public void NewProduct()
    {
        _productId = string.Empty;
        Detail = null;
        IsNewProduct = true;
        Name = string.Empty;
        Code = null;
        PriceMinor = 0;
        CostMinor = 0;
        OpeningStock = 0;
        Barcode = null;
        IsService = false;
        PriceChangeAllowed = true;
        SelectedGroup = null;
        SelectedBaseUnit = UnitOptions.FirstOrDefault();
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;
    public bool IsExistingProduct => !IsNewProduct;
    public bool ShowVariantEditor => !IsNewProduct && Detail is not null;
    public bool ShowNewProductHint => !ShowVariantEditor;
    public string EditorTitle => IsNewProduct ? "Produk baru" : "Edit produk";

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));
    partial void OnIsNewProductChanged(bool value)
    {
        OnPropertyChanged(nameof(IsExistingProduct));
        OnPropertyChanged(nameof(ShowVariantEditor));
        OnPropertyChanged(nameof(ShowNewProductHint));
        OnPropertyChanged(nameof(EditorTitle));
    }

    partial void OnDetailChanged(ProductDetail? value)
    {
        OnPropertyChanged(nameof(ShowVariantEditor));
        OnPropertyChanged(nameof(ShowNewProductHint));
    }

    [RelayCommand]
    private void ClearGroup() => SelectedGroup = null;

    public async Task InitializeAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            UnitOptions = new ObservableCollection<UnitOption>(
                await _units.ListActiveAsync());
            GroupOptions = new ObservableCollection<ProductGroup>(
                await _products.ListGroupsAsync());

            if (IsNewProduct || string.IsNullOrEmpty(_productId))
            {
                IsNewProduct = true;
                Detail = null;
                if (Name.Length == 0)
                {
                    Name = string.Empty;
                }

                SelectedBaseUnit ??= UnitOptions.FirstOrDefault();
                return;
            }

            var detail = await _products.GetProductDetailAsync(_productId);
            Detail = detail;
            Name = detail.Name;
            Code = detail.Code;
            PriceMinor = detail.PriceMinor;
            CostMinor = detail.CostMinor;
            Barcode = detail.Barcode;
            IsService = detail.IsService;
            PriceChangeAllowed = detail.PriceChangeAllowed;
            SelectedBaseUnit = UnitOptions.FirstOrDefault(unit => unit.Id == detail.BaseUnitId)
                ?? new UnitOption(detail.BaseUnitId, detail.BaseUnitName, null);
            SelectedGroup = GroupOptions.FirstOrDefault(group => group.Name == detail.GroupName);
            IsNewProduct = false;
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
    private async Task SaveProductAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length < 2)
        {
            ErrorMessage = "Nama produk minimal 2 karakter.";
            return;
        }

        if (IsNewProduct && SelectedBaseUnit is null)
        {
            ErrorMessage = "Pilih satuan dasar terlebih dahulu.";
            return;
        }

        if (PriceMinor < 0 || CostMinor < 0 || OpeningStock < 0)
        {
            ErrorMessage = "Harga, biaya, dan stok awal tidak boleh negatif.";
            return;
        }

        IsBusy = true;
        try
        {
            if (IsNewProduct)
            {
                var created = await _products.CreateAsync(new ProductCreateRequest(
                    Name.Trim(),
                    string.IsNullOrWhiteSpace(Code) ? null : Code.Trim(),
                    SelectedBaseUnit!.Id,
                    PriceMinor,
                    CostMinor,
                    checked(OpeningStock * 1000),
                    string.IsNullOrWhiteSpace(Barcode) ? null : Barcode.Trim(),
                    IsService,
                    PriceChangeAllowed,
                    SelectedGroup?.Name,
                    SelectedGroup?.Id));

                _productId = created.ProductId;
                IsNewProduct = false;
                InfoMessage = "Produk baru berhasil dibuat. Tambahkan varian satuan bila perlu.";
            }
            else
            {
                await _products.UpdateAsync(_productId, new ProductUpdateRequest(
                    Name.Trim(),
                    string.IsNullOrWhiteSpace(Code) ? null : Code.Trim(),
                    PriceMinor,
                    CostMinor,
                    IsService,
                    PriceChangeAllowed,
                    SelectedGroup?.Id));

                InfoMessage = "Perubahan produk disimpan.";
            }
        }
        catch (ConflictException)
        {
            ErrorMessage = "Kode produk sudah dipakai produk lain.";
            return;
        }
        catch (PosException exception)
        {
            ErrorMessage = exception.Message;
            return;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to save product");
            ErrorMessage = "Gagal menyimpan produk. Coba lagi.";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await InitializeAsync();
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

        if (NewUnitOption is null)
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
            await _products.CreateProductUnitAsync(new(
                _productId, NewUnitOption.Id, NewFactor, NewIsDefaultSale, NewPrice, 0));

            InfoMessage = "Varian satuan ditambahkan.";
            NewUnitOption = null;
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
