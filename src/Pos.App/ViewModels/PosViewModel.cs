using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Pos;
using Pos.Domain.Primitives;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class PosViewModel : ObservableObject
{
    private readonly ProductService _products;
    private readonly SaleService _sales;
    private readonly CustomerService _customers;
    private readonly PosCart _cart = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ProductLookup> _searchResults = new();

    [ObservableProperty]
    private ObservableCollection<CartLineViewModel> _lines = new();

    [ObservableProperty]
    private CartTotals _totals = CartTotals.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canCheckout;

    [ObservableProperty]
    private CustomerSummary? _selectedCustomer;

    [ObservableProperty]
    private string _customerLabel = "Pelanggan (umum)";

    [ObservableProperty]
    private DiscountKind _cartDiscountKind = DiscountKind.None;

    [ObservableProperty]
    private long _cartDiscountValue;

    private UserSession? _session;

    public PosViewModel(ProductService products, SaleService sales, CustomerService customers)
    {
        _products = products;
        _sales = sales;
        _customers = customers;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SearchText) && !string.IsNullOrWhiteSpace(SearchText))
            {
                _ = SearchAsync();
            }
            else if (e.PropertyName is nameof(CartDiscountKind) or nameof(CartDiscountValue))
            {
                _cart.SetCartDiscount(CartDiscountKind, CartDiscountValue);
                Recalculate();
                UpdateCanCheckout();
            }
        };
    }

    public void SetSession(UserSession session) => _session = session;

    public event Action? BackRequested;

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    private void UpdateCanCheckout() => CanCheckout = !IsBusy && _cart.Lines.Count > 0;

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;
        UpdateCanCheckout();
        try
        {
            var results = await _products.SearchAsync(SearchText, 20);
            SearchResults = new ObservableCollection<ProductLookup>(results);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Product search failed");
            ErrorMessage = "Pencarian produk gagal. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
            UpdateCanCheckout();
        }
    }

    [RelayCommand]
    private void AddToCart(ProductLookup? product)
    {
        if (product is null || IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        try
        {
            var line = _cart.Add(product, 1);
            ReplaceOrAddLine(line);
            Recalculate();
            SearchText = string.Empty;
            SearchResults = new ObservableCollection<ProductLookup>();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Add to cart failed");
            ErrorMessage = exception.Message;
        }
        finally
        {
            UpdateCanCheckout();
        }
    }

    [RelayCommand]
    private async Task SelectCustomerAsync()
    {
        // Delegate to shell which hosts the customer selection screen.
        // For now, show a simple inline selection.
        ErrorMessage = string.Empty;
        try
        {
            var customers = await _customers.ListActiveAsync();
            if (customers.Count == 0)
            {
                ErrorMessage = "Belum ada pelanggan aktif.";
                return;
            }

            // Simple selection: pick the first active customer for demo.
            // Full S10 screen integration is handled via shell navigation.
            var first = customers[0];
            SelectedCustomer = first;
            CustomerLabel = first.Name + (first.PriceListName is not null ? " · " + first.PriceListName : string.Empty);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load customers");
            ErrorMessage = "Gagal memuat daftar pelanggan. Coba lagi.";
        }
    }

    [RelayCommand]
    private void ClearCustomer()
    {
        SelectedCustomer = null;
        CustomerLabel = "Pelanggan (umum)";
    }

    private void ChangeQuantity(CartLineViewModel? line, long delta)
    {
        if (line is null || IsBusy)
        {
            return;
        }

        var newQty = line.QtyMilli + delta;
        if (newQty <= 0)
        {
            RemoveLineCommand.Execute(line);
            return;
        }

        _cart.SetQuantity(line.LineId, newQty);
        RefreshLines();
        Recalculate();
        UpdateCanCheckout();
    }

    [RelayCommand]
    private void RemoveLine(CartLineViewModel? line)
    {
        if (line is null)
        {
            return;
        }

        _cart.Remove(line.LineId);
        RefreshLines();
        Recalculate();
        UpdateCanCheckout();
    }

    [RelayCommand]
    private void IncreaseLine(CartLineViewModel? line) => ChangeQuantity(line, 1000);

    [RelayCommand]
    private void DecreaseLine(CartLineViewModel? line) => ChangeQuantity(line, -1000);

    private void SetLineUnit(CartLineViewModel? line, string productUnitId, string unitName, long factor, long price)
    {
        if (line is null || IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        try
        {
            var updatedLine = _cart.Lines.First(l => l.LineId == line.LineId);
            var newLine = updatedLine with
            {
                ProductUnitId = productUnitId,
                UnitName = unitName,
                UnitFactorMilli = factor,
                UnitPriceMinor = price,
                PriceOverridden = true,
            };
            _cart.ReplaceLine(newLine);
            RefreshLines();
            Recalculate();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to switch unit");
            ErrorMessage = exception.Message;
        }
        finally
        {
            UpdateCanCheckout();
        }
    }

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        if (IsBusy || _cart.Lines.Count == 0)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;
        UpdateCanCheckout();
        try
        {
            var request = new CheckoutRequest(
                _cart.Lines.Select(line => new SaleLineInput(
                    line.ProductId,
                    line.ProductUnitId,
                    line.QtyMilli,
                    line.PriceOverridden ? line.UnitPriceMinor : null,
                    line.DiscountKind,
                    line.DiscountValue,
                    null,
                    line.Comment)).ToList(),
                "pmt-cash",
                0,
                CustomerId: SelectedCustomer?.Id,
                CartDiscountKind: CartDiscountKind,
                CartDiscountValue: CartDiscountValue);

            var result = await _sales.CheckoutAsync(request);
            ErrorMessage = "Transaksi selesai. " + result.Number + " · Total " +
                MoneyMath.Format(result.TotalMinor) +
                (result.ChangeMinor > 0
                    ? " · Kembali " + MoneyMath.Format(result.ChangeMinor)
                    : string.Empty);
            _cart.Clear();
            Lines = new ObservableCollection<CartLineViewModel>();
            SelectedCustomer = null;
            CustomerLabel = "Pelanggan (umum)";
            CartDiscountKind = DiscountKind.None;
            CartDiscountValue = 0;
            Recalculate();
        }
        catch (PosException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Checkout failed");
            ErrorMessage = "Pembayaran gagal. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
            UpdateCanCheckout();
        }
    }

    private void ReplaceOrAddLine(CartLine line)
    {
        var existing = Lines.FirstOrDefault(l => l.LineId == line.LineId);
        if (existing is not null)
        {
            var index = Lines.IndexOf(existing);
            Lines[index] = existing.UpdateFrom(line);
        }
        else
        {
            Lines.Add(new CartLineViewModel(line));
        }
    }

    private void RefreshLines()
    {
        Lines = new ObservableCollection<CartLineViewModel>(
            _cart.Lines.Select(line => new CartLineViewModel(line)));
    }

    private void Recalculate() => Totals = _cart.Totals;
}

public sealed record CartLineViewModel(
    string LineId,
    string ProductId,
    string ProductUnitId,
    string Name,
    string? Code,
    string UnitName,
    long UnitFactorMilli,
    long QtyMilli,
    long BaseQtyMilli,
    long UnitPriceMinor,
    long DiscountMinor,
    long TotalMinor,
    DiscountKind DiscountKind,
    long DiscountValue,
    bool PriceOverridden,
    string? Comment)
{
    public CartLineViewModel(CartLine line) : this(
        line.LineId, line.ProductId, line.ProductUnitId, line.Name, line.Code,
        line.UnitName, line.UnitFactorMilli, line.QtyMilli, line.BaseQtyMilli,
        line.UnitPriceMinor, line.DiscountMinor, line.TotalMinor,
        line.DiscountKind, line.DiscountValue, line.PriceOverridden, line.Comment)
    {
    }

    public string DisplayText =>
        UnitMath.FormatQuantity(QtyMilli, UnitName) + " × " + MoneyMath.Format(UnitPriceMinor);

    public CartLineViewModel UpdateFrom(CartLine line) => this with
    {
        QtyMilli = line.QtyMilli,
        BaseQtyMilli = line.BaseQtyMilli,
        UnitPriceMinor = line.UnitPriceMinor,
        DiscountMinor = line.DiscountMinor,
        TotalMinor = line.TotalMinor,
        DiscountKind = line.DiscountKind,
        DiscountValue = line.DiscountValue,
        PriceOverridden = line.PriceOverridden,
    };
}
