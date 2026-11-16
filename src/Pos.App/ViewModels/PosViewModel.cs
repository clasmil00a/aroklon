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
    private readonly CashDrawerService _cashDrawer;
    private readonly ProductSearchViewModel _productSearch;
    private readonly PosCart _cart = new();
    private int _searchVersion;

    public ProductSearchViewModel ProductSearchVm => _productSearch;

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

    public long TotalTendered => SelectedPayments.Sum(p => p.AmountMinor);

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<long> _quickPayAmounts = new()
    {
        10000, 25000, 50000, 100000, 200000
    };

    [ObservableProperty]
    private bool _isSearchModalOpen;

    [ObservableProperty]
    private string _searchMode = "all";

    [ObservableProperty]
    private bool _isQuantityVisible;

    [ObservableProperty]
    private string _quantityText = "1";

    [ObservableProperty]
    private string _quantityError = string.Empty;

    [ObservableProperty]
    private bool _isDiscountVisible;

    [ObservableProperty]
    private string _discountPercentText = string.Empty;

    [ObservableProperty]
    private string _discountAmountText = string.Empty;

    [ObservableProperty]
    private string _discountError = string.Empty;

    [ObservableProperty]
    private bool _isCommentVisible;

    [ObservableProperty]
    private string _commentText = string.Empty;

    [ObservableProperty]
    private bool _isPaymentVisible;

    [ObservableProperty]
    private bool _isBarcodeVisible;

    [ObservableProperty]
    private bool _isCustomerVisible;

    [ObservableProperty]
    private string _customerSearchText = string.Empty;

    [ObservableProperty]
    private string _customerError = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CustomerSummary> _customerResults = new();

    private IReadOnlyList<CustomerSummary> _allCustomers = Array.Empty<CustomerSummary>();

    [ObservableProperty]
    private bool _isSearchResultsOpen;

    [ObservableProperty]
    private string _quantityLineName = string.Empty;

    [ObservableProperty]
    private string _quantityUnitLabel = string.Empty;

    [ObservableProperty]
    private bool _discountIsLine;

    [ObservableProperty]
    private string _discountTargetLabel = "Seluruh transaksi";

    [ObservableProperty]
    private string _paymentAmountText = string.Empty;

    [ObservableProperty]
    private string _paymentError = string.Empty;

    [ObservableProperty]
    private string _paymentChangeText = string.Empty;

    [ObservableProperty]
    private bool _isVoidConfirmVisible;

    [ObservableProperty]
    private string _parkedSaleLabel = string.Empty;

    [ObservableProperty]
    private int _parkedOrderCount;

    private string? _parkedSaleId;
    private UserSession? _session;

    public event Action? MenuRequested;
    public event Action? RefundRequested;
    public event Action? LockRequested;
    public event Action? OpenOrdersRequested;
    public event Action? SalesHistoryRequested;

    public string SearchHint => SearchMode == "code"
        ? "Cari kode / SKU produk"
        : "Cari produk berdasarkan nama";

    public PosViewModel(
        ProductService products,
        SaleService sales,
        CustomerService customers,
        CashDrawerService cashDrawer)
    {
        _products = products;
        _sales = sales;
        _customers = customers;
        _cashDrawer = cashDrawer;
        _productSearch = new ProductSearchViewModel(_products);
        _productSearch.ProductSelected += OnProductSearchProductSelected;
        _productSearch.CloseRequested += () => IsSearchModalOpen = false;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SearchText))
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    _searchVersion++;
                    SearchResults = new ObservableCollection<ProductLookup>();
                    IsSearchResultsOpen = false;
                }
                else
                {
                    _ = SearchAsync();
                }
            }
            else if (e.PropertyName is nameof(SearchMode))
            {
                OnPropertyChanged(nameof(SearchHint));
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

        _ = LoadPaymentTypesAsync();
    }

    public void SetSession(UserSession session) => _session = session;

    public async Task ResumeOpenOrder(OpenOrderResumeData data)
    {
        _cart.Clear();
        _cart.ClearCartDiscount();
        CartDiscountKind = DiscountKind.None;
        CartDiscountValue = 0;
        _parkedSaleId = data.SaleId;
        ParkedSaleLabel = data.Number;
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
    public bool HasCartDiscount => Totals.DiscountMinor > 0;
    public bool HasLines => Lines.Count > 0;
    public bool HasNoLines => !HasLines;
    public bool HasParkedOrders => ParkedOrderCount > 0;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));
    partial void OnPaymentAmountTextChanged(string value) => UpdatePaymentChange();

    partial void OnSelectedPaymentsChanged(
        ObservableCollection<PaymentSelectionViewModel> value)
    {
        if (value is not null)
        {
            value.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(TotalTendered));
                UpdateCanCheckout();
            };
        }

        OnPropertyChanged(nameof(TotalTendered));
        UpdateCanCheckout();
    }
    partial void OnParkedOrderCountChanged(int value) =>
        OnPropertyChanged(nameof(HasParkedOrders));

    public void UpdateCanCheckout()
    {
        CanCheckout = !IsBusy && _cart.Lines.Count > 0 && SelectedPayments.Count > 0;
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(HasNoLines));
        OnPropertyChanged(nameof(HasParkedOrders));
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    [RelayCommand]
    private async Task KickCashDrawerAsync()
    {
        try
        {
            await _cashDrawer.KickAsync();
            InfoMessage = "Laci kas ditekan.";
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Cash drawer kick failed");
            ErrorMessage = "Gagal membuka laci kas. Coba lagi.";
        }
    }

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
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        var version = ++_searchVersion;
        ErrorMessage = string.Empty;
        try
        {
            var results = await _products.SearchAsync(SearchText, 20);
            if (version != _searchVersion)
            {
                return;
            }

            SearchResults = new ObservableCollection<ProductLookup>(results);
            IsSearchResultsOpen = results.Count > 0;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Product search failed");
            if (version == _searchVersion)
            {
                ErrorMessage = "Pencarian produk gagal. Coba lagi.";
            }
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

            var line = _cart.Add(product, 1000);
            ReplaceOrAddLine(line);
            Recalculate();
            BarcodeInput = string.Empty;
            IsBarcodeVisible = false;
            SearchText = string.Empty;
            SearchResults = new ObservableCollection<ProductLookup>();
            IsSearchResultsOpen = false;
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
            var line = _cart.Add(product, 1000);
            ReplaceOrAddLine(line);
            Recalculate();
            SearchText = string.Empty;
            SearchResults = new ObservableCollection<ProductLookup>();
            IsSearchResultsOpen = false;
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
    private void OpenSearchModal()
    {
        IsSearchModalOpen = true;
        _ = _productSearch.InitializeAsync();
    }

    private void OnProductSearchProductSelected(ProductLookup? product)
    {
        if (product is null) return;
        AddProductToCart(product, 1_000);
        IsSearchModalOpen = false;
        _productSearch.SearchText = string.Empty;
        _productSearch.Results = new ObservableCollection<ProductLookup>();
        _productSearch.SelectedIndex = -1;
        InfoMessage = $"{product.Name} ditambahkan ke keranjang.";
    }

    [RelayCommand]
    private void OpenMenu() => MenuRequested?.Invoke();

    [RelayCommand]
    private void OpenRefund() => RefundRequested?.Invoke();

    [RelayCommand]
    private void LockScreen() => LockRequested?.Invoke();

    [RelayCommand]
    private void OpenOrders() => OpenOrdersRequested?.Invoke();

    [RelayCommand]
    private void OpenHistory() => SalesHistoryRequested?.Invoke();

    [RelayCommand]
    private void ToggleSearchMode()
    {
        SearchMode = SearchMode == "code" ? "all" : "code";
        OnPropertyChanged(nameof(SearchHint));
    }

    [RelayCommand]
    private void ShowBarcode()
    {
        BarcodeInput = string.Empty;
        IsBarcodeVisible = true;
    }

    [RelayCommand]
    private void HideBarcode() => IsBarcodeVisible = false;

    [RelayCommand]
    private async Task SubmitSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        if (SearchResults.Count == 0)
        {
            await SearchAsync();
        }

        var first = SearchResults.FirstOrDefault();
        if (first is null)
        {
            ErrorMessage = "Produk tidak ditemukan.";
            return;
        }

        AddProductToCart(first, 1_000);
        SearchText = string.Empty;
        SearchResults = new ObservableCollection<ProductLookup>();
        IsSearchResultsOpen = false;
    }

    [RelayCommand]
    private void ShowQuantity()
    {
        if (SelectedLine is null)
        {
            SelectedLine = Lines.FirstOrDefault();
        }

        if (SelectedLine is null)
        {
            ErrorMessage = "Pilih baris keranjang terlebih dahulu.";
            return;
        }

        QuantityLineName = SelectedLine.Name;
        QuantityUnitLabel = SelectedLine.UnitName;
        QuantityText = (SelectedLine.QtyMilli / 1000m).ToString(
            "0.###",
            System.Globalization.CultureInfo.GetCultureInfo("id-ID"));
        QuantityError = string.Empty;
        IsQuantityVisible = true;
    }

    [RelayCommand]
    private void HideQuantity() => IsQuantityVisible = false;

    [RelayCommand]
    private void ApplyQuantity()
    {
        if (SelectedLine is null)
        {
            return;
        }

        if (!UnitMath.TryParseQuantity(QuantityText, out var qtyMilli) || qtyMilli <= 0)
        {
            QuantityError = "Jumlah harus lebih dari nol.";
            return;
        }

        try
        {
            _cart.SetQuantity(SelectedLine.LineId, qtyMilli);
            var lineId = SelectedLine.LineId;
            RefreshLines();
            SelectedLine = Lines.FirstOrDefault(line => line.LineId == lineId);
            Recalculate();
            IsQuantityVisible = false;
        }
        catch (Exception exception)
        {
            QuantityError = exception.Message;
        }
        finally
        {
            UpdateCanCheckout();
        }
    }

    [RelayCommand]
    private void ShowDiscount()
    {
        DiscountIsLine = SelectedLine is not null;
        DiscountTargetLabel = SelectedLine is null
            ? "Seluruh transaksi"
            : "Baris: " + SelectedLine.Name;

        var kind = SelectedLine?.DiscountKind ?? CartDiscountKind;
        var value = SelectedLine?.DiscountValue ?? CartDiscountValue;
        DiscountPercentText = kind == DiscountKind.Percent
            ? (value / 100m).ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("id-ID"))
            : string.Empty;
        DiscountAmountText = kind == DiscountKind.Amount
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
        DiscountError = string.Empty;
        IsDiscountVisible = true;
    }

    [RelayCommand]
    private void HideDiscount() => IsDiscountVisible = false;

    [RelayCommand]
    private void ApplyDiscount()
    {
        var hasPercent = !string.IsNullOrWhiteSpace(DiscountPercentText);
        var hasAmount = !string.IsNullOrWhiteSpace(DiscountAmountText);
        if (hasPercent && hasAmount)
        {
            DiscountError = "Isi salah satu: persen atau nominal.";
            return;
        }

        var kind = DiscountKind.None;
        var value = 0L;
        if (hasPercent)
        {
            if (!decimal.TryParse(
                    DiscountPercentText,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.GetCultureInfo("id-ID"),
                    out var percent) ||
                percent < 0 ||
                percent > 100)
            {
                DiscountError = "Persen diskon harus antara 0 dan 100.";
                return;
            }

            kind = DiscountKind.Percent;
            value = (long)decimal.Round(percent * 100m, 0, MidpointRounding.AwayFromZero);
        }
        else if (hasAmount)
        {
            if (!TryParseMoney(DiscountAmountText, out value) || value < 0)
            {
                DiscountError = "Nominal diskon tidak valid.";
                return;
            }

            kind = DiscountKind.Amount;
        }

        try
        {
            if (DiscountIsLine && SelectedLine is not null)
            {
                _cart.SetDiscount(SelectedLine.LineId, kind, value);
                var lineId = SelectedLine.LineId;
                RefreshLines();
                SelectedLine = Lines.FirstOrDefault(line => line.LineId == lineId);
            }
            else
            {
                CartDiscountKind = kind;
                CartDiscountValue = value;
            }

            Recalculate();
            IsDiscountVisible = false;
        }
        catch (Exception exception)
        {
            DiscountError = exception.Message;
        }
        finally
        {
            UpdateCanCheckout();
        }
    }

    [RelayCommand]
    private void ShowComment()
    {
        if (SelectedLine is null)
        {
            SelectedLine = Lines.FirstOrDefault();
        }

        if (SelectedLine is null)
        {
            ErrorMessage = "Pilih baris keranjang terlebih dahulu.";
            return;
        }

        CommentText = SelectedLine.Comment ?? string.Empty;
        IsCommentVisible = true;
    }

    [RelayCommand]
    private void HideComment() => IsCommentVisible = false;

    [RelayCommand]
    private void ApplyComment()
    {
        if (SelectedLine is null)
        {
            return;
        }

        try
        {
            _cart.SetComment(
                SelectedLine.LineId,
                string.IsNullOrWhiteSpace(CommentText) ? null : CommentText.Trim());
            var lineId = SelectedLine.LineId;
            RefreshLines();
            SelectedLine = Lines.FirstOrDefault(line => line.LineId == lineId);
            IsCommentVisible = false;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    [RelayCommand]
    private async Task ShowPaymentAsync()
    {
        if (_cart.Lines.Count == 0)
        {
            ErrorMessage = "Keranjang masih kosong.";
            return;
        }

        // A saved non-cash payment (card/transfer) already covers the sale.
        if (SelectedPayments.Count > 0 &&
            SelectedPayments.All(payment => !payment.MarkAsPaid) &&
            SelectedPayments.Sum(payment => payment.AmountMinor) >= Totals.TotalMinor)
        {
            await CheckoutAsync();
            return;
        }

        PaymentAmountText = Totals.TotalMinor.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        PaymentError = string.Empty;
        UpdatePaymentChange();
        IsPaymentVisible = true;
    }

    [RelayCommand]
    private void HidePayment() => IsPaymentVisible = false;

    [RelayCommand]
    private void SetPaymentAmount(long amount)
    {
        PaymentAmountText = amount.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        PaymentError = string.Empty;
        UpdatePaymentChange();
    }

    [RelayCommand]
    private void SetExactPayment() =>
        SetPaymentAmount(Totals.TotalMinor);

    [RelayCommand]
    private async Task ApplyPaymentAsync()
    {
        if (!TryParseMoney(PaymentAmountText, out var amount) || amount <= 0)
        {
            PaymentError = "Jumlah pembayaran tidak valid.";
            return;
        }

        if (amount < Totals.TotalMinor)
        {
            PaymentError = "Jumlah pembayaran kurang dari total.";
            return;
        }

        var type = PaymentTypes.FirstOrDefault(t => t.MarkAsPaid && t.IsDefault)
            ?? PaymentTypes.FirstOrDefault(t => t.MarkAsPaid)
            ?? PaymentTypes.FirstOrDefault();
        if (type is null)
        {
            PaymentError = "Belum ada tipe pembayaran. Hubungi administrator.";
            return;
        }

        SelectedPayments = new ObservableCollection<PaymentSelectionViewModel>
        {
            new(type) { AmountMinor = amount }
        };
        IsPaymentVisible = false;
        PaymentError = string.Empty;
        await CheckoutAsync();
    }

    [RelayCommand]
    private void ChoosePaymentType(PaymentTypeInfo? type)
    {
        if (type is null || IsBusy)
        {
            return;
        }

        SelectedPayments = new ObservableCollection<PaymentSelectionViewModel>
        {
            new(type) { AmountMinor = Totals.TotalMinor }
        };
        PaymentError = string.Empty;

        if (type.MarkAsPaid)
        {
            PaymentAmountText = Totals.TotalMinor.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            UpdatePaymentChange();
            IsPaymentVisible = true;
        }

        UpdateCanCheckout();
    }

    [RelayCommand]
    private void ShowVoidConfirm()
    {
        if (_cart.Lines.Count == 0)
        {
            return;
        }

        IsVoidConfirmVisible = true;
    }

    [RelayCommand]
    private void CancelVoidConfirm() => IsVoidConfirmVisible = false;

    [RelayCommand]
    private void ConfirmVoid()
    {
        ClearSale();
        IsVoidConfirmVisible = false;
        InfoMessage = "Pesanan dibatalkan.";
    }

    [RelayCommand]
    private void NewSale()
    {
        if (_cart.Lines.Count == 0 && _parkedSaleId is null)
        {
            SearchText = string.Empty;
            return;
        }

        ClearSale();
        InfoMessage = "Penjualan baru dimulai.";
    }

    [RelayCommand]
    private async Task SaveSaleAsync()
    {
        if (IsBusy || _cart.Lines.Count == 0)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _sales.ParkAsync(new ParkSaleRequest(
                _cart.Lines.Select(line => new SaleLineInput(
                    line.ProductId,
                    line.ProductUnitId,
                    line.QtyMilli,
                    line.PriceOverridden ? line.UnitPriceMinor : null,
                    line.DiscountKind,
                    line.DiscountValue,
                    null,
                    line.Comment)).ToList(),
                SelectedCustomer?.Id,
                Note: null));

            InfoMessage = "Pesanan disimpan: " + result.Number;
            ParkedSaleLabel = result.Number;
            ParkedOrderCount++;
            ClearSale();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to park sale");
            ErrorMessage = "Gagal menyimpan pesanan. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
            UpdateCanCheckout();
        }
    }

    [RelayCommand]
    private void CloseDialogs()
    {
        IsSearchResultsOpen = false;
        IsQuantityVisible = false;
        IsDiscountVisible = false;
        IsCommentVisible = false;
        IsPaymentVisible = false;
        IsPriceOverrideVisible = false;
        IsBarcodeVisible = false;
        IsVoidConfirmVisible = false;
        IsSearchModalOpen = false;
        IsCustomerVisible = false;
    }

    private void ClearSale()
    {
        _cart.Clear();
        _cart.ClearCartDiscount();
        _parkedSaleId = null;
        ParkedSaleLabel = string.Empty;
        Lines = new ObservableCollection<CartLineViewModel>();
        SelectedLine = null;
        SelectedCustomer = null;
        CustomerLabel = "Pelanggan (umum)";
        CartDiscountKind = DiscountKind.None;
        CartDiscountValue = 0;
        SelectedPayments = new ObservableCollection<PaymentSelectionViewModel>();
        SearchText = string.Empty;
        SearchResults = new ObservableCollection<ProductLookup>();
        IsSearchResultsOpen = false;
        PaymentAmountText = string.Empty;
        PaymentChangeText = string.Empty;
        Recalculate();
        UpdateCanCheckout();
    }

    private void UpdatePaymentChange()
    {
        if (!TryParseMoney(PaymentAmountText, out var amount))
        {
            PaymentChangeText = string.Empty;
            return;
        }

        var change = amount - Totals.TotalMinor;
        PaymentChangeText = change > 0 ? MoneyMath.Format(change) : string.Empty;
    }

    private static bool TryParseMoney(string? text, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = text
            .Trim()
            .Replace("Rp", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);
        return long.TryParse(cleaned, out value) && value >= 0;
    }

    [RelayCommand]
    private async Task ShowCustomerAsync()
    {
        CustomerSearchText = string.Empty;
        CustomerError = string.Empty;
        IsCustomerVisible = true;
        try
        {
            _allCustomers = await _customers.ListActiveAsync();
            FilterCustomers();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load customers");
            CustomerError = "Gagal memuat daftar pelanggan. Coba lagi.";
        }
    }

    [RelayCommand]
    private void HideCustomer() => IsCustomerVisible = false;

    [RelayCommand]
    private void ApplyCustomer(CustomerSummary? customer)
    {
        SelectedCustomer = customer;
        CustomerLabel = customer is null
            ? "Pelanggan (umum)"
            : customer.Name +
              (customer.PriceListName is not null ? " · " + customer.PriceListName : string.Empty);
        IsCustomerVisible = false;
    }

    [RelayCommand]
    private void ClearCustomer()
    {
        SelectedCustomer = null;
        CustomerLabel = "Pelanggan (umum)";
    }

    private void FilterCustomers()
    {
        var query = CustomerSearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allCustomers
            : _allCustomers.Where(customer =>
                customer.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (customer.Code?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        CustomerResults = new ObservableCollection<CustomerSummary>(filtered);
    }

    partial void OnCustomerSearchTextChanged(string value) => FilterCustomers();

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

    [ObservableProperty]
    private CartLineViewModel? _selectedLine;

    [ObservableProperty]
    private long _newLinePrice;

    [ObservableProperty]
    private string _priceOverrideError = string.Empty;

    [ObservableProperty]
    private bool _isPriceOverrideVisible;

    [RelayCommand]
    private void ShowPriceOverride(CartLineViewModel? line)
    {
        if (line is null || IsBusy) return;

        SelectedLine = line;
        NewLinePrice = line.UnitPriceMinor;
        PriceOverrideError = string.Empty;
        IsPriceOverrideVisible = true;
    }

    [RelayCommand]
    private void HidePriceOverride()
    {
        IsPriceOverrideVisible = false;
        SelectedLine = null;
        NewLinePrice = 0;
        PriceOverrideError = string.Empty;
    }

    [RelayCommand]
    private void ApplyPriceOverride()
    {
        if (SelectedLine is null || IsBusy) return;

        if (NewLinePrice < 0)
        {
            PriceOverrideError = "Harga tidak boleh negatif.";
            return;
        }

        ErrorMessage = string.Empty;
        try
        {
            var updatedLine = _cart.Lines.First(l => l.LineId == SelectedLine.LineId);
            var newLine = updatedLine with
            {
                UnitPriceMinor = NewLinePrice,
                PriceOverridden = true,
            };
            _cart.ReplaceLine(newLine);
            RefreshLines();
            Recalculate();
            HidePriceOverride();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to change price");
            PriceOverrideError = exception.Message;
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
        if (paymentType == null && PaymentTypes.Count > 0)
        {
            paymentType = PaymentTypes.First().Id;
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

        var selectedType = PaymentTypes.FirstOrDefault(p => p.Id == paymentType);
        if (selectedType is null) return;

        var vm = new PaymentSelectionViewModel(selectedType);
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
                CartDiscountValue: CartDiscountValue,
                ParkedSaleId: _parkedSaleId);

            var result = await _sales.CheckoutAsync(request);
            InfoMessage = "Transaksi selesai. " + result.Number + " · Total " +
                MoneyMath.Format(result.TotalMinor) +
                (result.ChangeMinor > 0
                    ? " · Kembali " + MoneyMath.Format(result.ChangeMinor)
                    : string.Empty);
            ErrorMessage = string.Empty;

            // Show receipt after checkout (S11).
            await ShowReceiptAsync(result);

            ClearSale();
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
        var selectedId = SelectedLine?.LineId;
        Lines = new ObservableCollection<CartLineViewModel>(
            _cart.Lines.Select(line => new CartLineViewModel(line)));
        SelectedLine = selectedId is null
            ? null
            : Lines.FirstOrDefault(line => line.LineId == selectedId);
    }

    private void Recalculate()
    {
        Totals = _cart.Totals;
        OnPropertyChanged(nameof(HasCartDiscount));
    }

    public void AddProductToCart(ProductLookup product, long qtyMilli)
    {
        if (product is null || IsBusy) return;
        ErrorMessage = string.Empty;
        try
        {
            var line = _cart.Add(product, qtyMilli);
            ReplaceOrAddLine(line);
            Recalculate();
            UpdateCanCheckout();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Add to cart failed");
            ErrorMessage = exception.Message;
        }
    }

    public void RecalculateAndUpdate()
    {
        Recalculate();
        UpdateCanCheckout();
    }
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

    public string QuantityDisplay => UnitMath.FormatQuantity(QtyMilli);

    public string SubTitle => string.IsNullOrWhiteSpace(Code)
        ? UnitName
        : Code + " · " + UnitName;

    public bool HasDiscount => DiscountMinor > 0;

    public string DiscountDisplay => HasDiscount
        ? "-" + MoneyMath.Format(DiscountMinor)
        : string.Empty;

    public bool HasComment => !string.IsNullOrWhiteSpace(Comment);

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
