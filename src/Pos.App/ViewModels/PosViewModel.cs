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
    private string _barcodeInput = string.Empty;

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

    [ObservableProperty]
    private ObservableCollection<PaymentTypeInfo> _paymentTypes = new();

    [ObservableProperty]
    private ObservableCollection<PaymentSelectionViewModel> _selectedPayments = new();

    public long TotalTendered => _selectedPayments.Sum(p => p.AmountMinor);

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<long> _quickPayAmounts = new()
    {
        10000, 25000, 50000, 100000, 200000
    };

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
            else if (e.PropertyName is nameof(SelectedPayments))
            {
                OnPropertyChanged(nameof(TotalTendered));
                UpdateCanCheckout();
            }
        };

        _selectedPayments.CollectionChanged += (_, e) =>
        {
            OnPropertyChanged(nameof(TotalTendered));
            UpdateCanCheckout();
        };

        _ = LoadPaymentTypesAsync();
    }

    public void SetSession(UserSession session) => _session = session;

    public async Task ResumeOpenOrder(OpenOrderResumeData data)
    {
        _cart.Clear();
        Lines = new ObservableCollection<CartLineViewModel>();

        foreach (var input in data.Lines)
        {
            // Look up current product info for display; prices are re-resolved at checkout.
            var lookup = await _products.GetAsync(input.ProductId);
            var line = _cart.Add(lookup, input.QtyMilli,
                input.UnitPriceOverrideMinor,
                input.DiscountKind,
                input.DiscountValue,
                input.Comment);
        }

        Recalculate();
        UpdateCanCheckout();
    }

    public event Action? BackRequested;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    private void UpdateCanCheckout() => CanCheckout = !IsBusy && _cart.Lines.Count > 0 && _selectedPayments.Count > 0;

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    [RelayCommand]
    private async Task LoadPaymentTypesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var types = await _sales.ListPaymentTypesAsync();
            PaymentTypes = new ObservableCollection<PaymentTypeInfo>(types);

            // Select default payment type (cash).
            var defaultType = types.FirstOrDefault(t => t.IsDefault) ?? types.FirstOrDefault();
            if (defaultType is not null)
            {
                SelectedPayments = new ObservableCollection<PaymentSelectionViewModel>
                {
                    new(defaultType)
                };
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load payment types");
            ErrorMessage = "Gagal memuat tipe pembayaran. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsBusy) return;
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
    private async Task ScanBarcodeAsync()
    {
        if (IsBusy) return;
        ErrorMessage = string.Empty;
        IsBusy = true;
        UpdateCanCheckout();
        try
        {
            var barcode = BarcodeInput?.Trim();
            if (string.IsNullOrEmpty(barcode))
            {
                ErrorMessage = "Barcode kosong. Silakan scan atau ketik barcode.";
                return;
            }

            var product = await _products.FindByBarcodeAsync(barcode);
            if (product is null)
            {
                ErrorMessage = $"Barcode '{barcode}' tidak ditemukan.";
                return;
            }

            var line = _cart.Add(product, 1);
            ReplaceOrAddLine(line);
            Recalculate();
            BarcodeInput = string.Empty;
            SearchText = string.Empty;
            SearchResults = new ObservableCollection<ProductLookup>();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Barcode scan failed");
            ErrorMessage = "Pindai barcode gagal. Coba lagi.";
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
        if (product is null || IsBusy) return;
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
        ErrorMessage = string.Empty;
        try
        {
            var customers = await _customers.ListActiveAsync();
            if (customers.Count == 0)
            {
                ErrorMessage = "Belum ada pelanggan aktif.";
                return;
            }

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
        if (line is null || IsBusy) return;

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
        if (line is null) return;
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
        if (line is null || IsBusy) return;

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
    private void AddPaymentType(PaymentTypeInfo? type)
    {
        if (type is null || IsBusy) return;

        // Don't add duplicate payment type
        if (SelectedPayments.Any(p => p.PaymentTypeId == type.Id)) return;

        var vm = new PaymentSelectionViewModel(type);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PaymentSelectionViewModel.AmountMinor))
            {
                OnPropertyChanged(nameof(TotalTendered));
                UpdateCanCheckout();
            }
        };
        SelectedPayments.Add(vm);
        UpdateCanCheckout();
    }

    [RelayCommand]
    private void QuickAddPaymentAmount(long amount)
    {
        if (IsBusy) return;

        // Find first payment type that is MarkAsPaid (cash-type), default to first payment type
        var paymentType = SelectedPayments.FirstOrDefault()?.PaymentTypeId;
        if (paymentType == null && _paymentTypes.Count > 0)
        {
            paymentType = _paymentTypes.First().Id;
        }

        if (paymentType == null) return;

        // Don't add duplicate payment type entries with same type
        var existing = SelectedPayments.FirstOrDefault(p => p.PaymentTypeId == paymentType);
        if (existing != null)
        {
            // Add to existing amount
            existing.AmountMinor += amount;
            OnPropertyChanged(nameof(TotalTendered));
            UpdateCanCheckout();
            return;
        }

        var vm = new PaymentSelectionViewModel(_paymentTypes.FirstOrDefault(p => p.Id == paymentType) ?? _paymentTypes.FirstOrDefault());
        vm.AmountMinor = amount;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PaymentSelectionViewModel.AmountMinor))
            {
                OnPropertyChanged(nameof(TotalTendered));
                UpdateCanCheckout();
            }
        };
        SelectedPayments.Add(vm);
        UpdateCanCheckout();
    }

    [RelayCommand]
    private void RemovePayment(PaymentSelectionViewModel? payment)
    {
        if (payment is null) return;
        SelectedPayments.Remove(payment);
        UpdateCanCheckout();
    }

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        if (IsBusy || _cart.Lines.Count == 0) return;

        if (SelectedPayments.Count == 0)
        {
            ErrorMessage = "Minimal satu pembayaran wajib dipilih.";
            return;
        }

        var totalTendered = SelectedPayments.Sum(p => p.AmountMinor);
        if (totalTendered < Totals.TotalMinor)
        {
            ErrorMessage = "Jumlah pembayaran kurang dari total transaksi.";
            return;
        }

        var creditPayments = SelectedPayments.Where(p => !p.PaymentType.MarkAsPaid).ToList();
        if (creditPayments.Any() && SelectedCustomer is null)
        {
            ErrorMessage = "Pembayaran kredit memerlukan pelanggan.";
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
                SelectedPayments.Select(p => new PaymentInput(
                    p.PaymentTypeId,
                    p.AmountMinor)).ToList(),
                CustomerId: SelectedCustomer?.Id,
                CartDiscountKind: CartDiscountKind,
                CartDiscountValue: CartDiscountValue);

            var result = await _sales.CheckoutAsync(request);
            ErrorMessage = "Transaksi selesai. " + result.Number + " · Total " +
                MoneyMath.Format(result.TotalMinor) +
                (result.ChangeMinor > 0
                    ? " · Kembali " + MoneyMath.Format(result.ChangeMinor)
                    : string.Empty);

            // Show receipt after checkout (S11).
            await ShowReceiptAsync(result);

            _cart.Clear();
            Lines = new ObservableCollection<CartLineViewModel>();
            SelectedCustomer = null;
            CustomerLabel = "Pelanggan (umum)";
            CartDiscountKind = DiscountKind.None;
            CartDiscountValue = 0;
            SelectedPayments = new ObservableCollection<PaymentSelectionViewModel>();
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

    private async Task ShowReceiptAsync(SaleResult result)
    {
        try
        {
            var detail = await _sales.GetAsync(result.SaleId);
            var document = await _sales.GetReceiptAsync(detail.Id);
            var text = SaleService.RenderReceipt(document);
            Log.Information("Receipt for {Number}:\n{Receipt}", result.Number, text);
            InfoMessage = "Struk telah dicetak. Lihat riwayat penjualan untuk detail.";
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Could not generate receipt preview");
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
