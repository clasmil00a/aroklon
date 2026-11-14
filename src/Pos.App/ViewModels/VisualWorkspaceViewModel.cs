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

public sealed partial class VisualWorkspaceViewModel : ObservableObject
{
    private readonly ProductService _products;
    private readonly SaleService _sales;
    private readonly CustomerService _customers;
    private readonly PosCart _cart = new();

    [ObservableProperty]
    private ObservableCollection<ProductLookup> _productGroups = new();

    [ObservableProperty]
    private ObservableCollection<ProductLookup> _productList = new();

    [ObservableProperty]
    private ObservableCollection<CartLineViewModel> _lines = new();

    [ObservableProperty]
    private CartTotals _totals = CartTotals.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

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
    private long _tenderedAmount;

    [ObservableProperty]
    private long _changeAmount;

    private UserSession? _session;

    public VisualWorkspaceViewModel(ProductService products, SaleService sales, CustomerService customers)
    {
        _products = products;
        _sales = sales;
        _customers = customers;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CartDiscountKind) or nameof(CartDiscountValue))
            {
                _cart.SetCartDiscount(CartDiscountKind, CartDiscountValue);
                Recalculate();
                UpdateCanCheckout();
            }
            else if (e.PropertyName is nameof(TenderedAmount))
            {
                UpdateChange();
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

        _ = LoadAsync();
    }

    public void SetSession(UserSession session) => _session = session;

    public event Action? BackRequested;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            // Load product groups (top-level categories shown as tiles)
            var groups = await _products.SearchAsync(string.Empty, 50);
            ProductGroups = new ObservableCollection<ProductLookup>(groups.Take(12));

            // Load payment types
            var types = await _sales.ListPaymentTypesAsync();
            PaymentTypes = new ObservableCollection<PaymentTypeInfo>(types);

            // Select default payment type
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
            Log.Error(exception, "Failed to load visual workspace");
            ErrorMessage = "Gagal memuat layar penjualan. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    [RelayCommand]
    private async Task SelectProductGroup(ProductLookup? group)
    {
        if (group is null || IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var products = await _products.SearchAsync(group.Name ?? string.Empty, 50);
            ProductList = new ObservableCollection<ProductLookup>(products);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load product group");
            ErrorMessage = "Gagal memuat produk grup. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
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

    private void UpdateChange()
    {
        ChangeAmount = TenderedAmount > Totals.TotalMinor
            ? TenderedAmount - Totals.TotalMinor
            : 0;
    }

    private void UpdateCanCheckout() => CanCheckout = !IsBusy && _cart.Lines.Count > 0 && SelectedPayments.Count > 0;
}

public sealed partial class PaymentSelectionViewModel : ObservableObject
{
    public PaymentSelectionViewModel(PaymentTypeInfo paymentType)
    {
        PaymentType = paymentType;
    }

    public PaymentTypeInfo PaymentType { get; }

    public string PaymentTypeId => PaymentType.Id;
    public string PaymentTypeName => PaymentType.Name;
    public bool MarkAsPaid => PaymentType.MarkAsPaid;

    [ObservableProperty]
    private long _amountMinor;
}
