using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Database;
using Pos.Data.Services;
using Pos.Domain.Models;
using Pos.Domain.Security;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly ThemeService _theme;
    private readonly UserPreferencesService _preferences;
    private readonly BackupService _backups;
    private readonly DatabaseOptions _databaseOptions;
    private readonly DatabaseInfo _databaseInfo;
    private CompanyInfo _company;
    private readonly ProductService _products;
    private readonly SaleService _sales;
    private readonly UnitService _units;
    private readonly PriceListService _priceLists;
    private readonly CustomerService _customers;
    private readonly CashMovementService _cashMovements;
    private readonly RefundService _refunds;
    private readonly EndOfDayService _endOfDay;
    private readonly PaymentTypeService _paymentTypes;
    private readonly AdminUserService _users;
    private readonly DatabaseService _database;

    private ManagementViewModel? _management;
    private SettingsViewModel? _settings;
    private PosViewModel? _pos;
    private UnitViewModel? _unit;
    private ProductListViewModel? _productList;
    private ProductEditorViewModel? _productEditor;
    private PriceListViewModel? _priceList;
    private PriceListEditorViewModel? _priceListEditor;
    private ProductPriceViewModel? _productPrice;
    private CustomerSelectionViewModel? _customerSelection;
    private CashMovementViewModel? _cashMovement;
    private RefundViewModel? _refund;
    private EndOfDayViewModel? _endOfDayScreen;
    private OpenOrdersViewModel? _openOrders;
    private SalesHistoryViewModel? _salesHistory;
    private PaymentTypeViewModel? _paymentType;
    private UserManagementViewModel? _userManagement;
    private DatabaseViewModel? _databaseView;
    private VisualWorkspaceViewModel? _visualWorkspace;
    private ProductSearchViewModel? _productSearchPage;

    private ProductGroupViewModel? _productGroup;
    private ProductGroupService _productGroupService;
    private readonly SettingsService _settingsService;
    private readonly ReportsService _reportsService;
    private readonly TaxService _taxService;
    private readonly StockAdjustmentService _stockAdjustmentService;
    private readonly CashDrawerService _cashDrawer;
    private readonly AroniumImportService _aroniumImport;
    private readonly PrinterService _printerService = new();

    private ReportsViewModel? _reportsViewModel;
    private TaxRateViewModel? _taxRate;
    private StockAdjustmentViewModel? _stockAdjustment;
    private ReceiptViewModel? _receiptViewModelForReprint;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private string _activeRoute = "S02";

    [ObservableProperty]
    private string _pageTitle = "Kasir";

    [ObservableProperty]
    private bool _isMenuOpen;

    public string MenuHeader => "Aroklon - " + UserDisplayName;

    public string MenuDate => DateTime.Now.ToString("dd/MM/yyyy");

    public IReadOnlyList<ShellMenuItem> MenuItems { get; }

    public ShellViewModel(
        UserSession session,
        CompanyInfo company,
        DatabaseInfo databaseInfo,
        AuthService auth,
        ThemeService theme,
        UserPreferencesService preferences,
        BackupService backups,
        DatabaseOptions databaseOptions,
        ProductService products,
        SaleService sales,
        UnitService units,
        PriceListService priceLists,
        CustomerService customers,
        CashMovementService cashMovements,
        RefundService refunds,
        EndOfDayService endOfDay,
        PaymentTypeService paymentTypes,
        AdminUserService users,
        DatabaseService database,
        ProductGroupService productGroupService,
        SettingsService settings,
        ReportsService reportsService,
        TaxService taxService,
        StockAdjustmentService stockAdjustmentService,
        CashDrawerService cashDrawer,
        AroniumImportService aroniumImport)
    {
        Session = session;
        _company = company;
        _databaseInfo = databaseInfo;
        _auth = auth;
        _theme = theme;
        _preferences = preferences;
        _backups = backups;
        _databaseOptions = databaseOptions;
        _products = products;
        _sales = sales;
        _units = units;
        _priceLists = priceLists;
        _customers = customers;
        _cashMovements = cashMovements;
        _refunds = refunds;
        _endOfDay = endOfDay;
        _paymentTypes = paymentTypes;
        _users = users;
        _database = database;
        _productGroupService = productGroupService;
        _settingsService = settings;
        _reportsService = reportsService;
        _reportsService = reportsService;
        _taxService = taxService;
        _stockAdjustmentService = stockAdjustmentService;
        _cashDrawer = cashDrawer;
        _aroniumImport = aroniumImport;
        _openOrders = new OpenOrdersViewModel(_sales);
        _openOrders.ResumeRequested += resumeData =>
        {
            EnsurePos().ResumeOpenOrder(resumeData);
            Navigate("S02");
        };

        var menuItems = new List<ShellMenuItem>
        {
            new("Manajemen", "management", "\uE90F"),
            new(
                "Riwayat penjualan",
                "S32",
                "\uE73E",
                SeparatorBefore: true),
            new("Pesanan terbuka", "S12", "\uE81E"),
            new("Kas masuk / keluar", "S31", "\uEC59"),
            new("Pembayaran kredit", "credit", "\uE8C7"),
            new("Tutup hari", "S35", "\uE9D2"),
            new(
                "Info pengguna",
                "S25",
                "\uE77B",
                Section: "Pengguna"),
            new("Keluar", "signout", "\uE72A"),
            new(
                "Masukan",
                "feedback",
                "\uE789",
                SeparatorBefore: true)
        };

        MenuItems = menuItems;

        Navigate("S02");
    }

    public UserSession Session { get; }

    public string CompanyName => _company.Name;

    public string UserDisplayName => Session.FullName;

    public string RoleName => Session.RoleName;

    public string Initials => BuildInitials(Session.FullName, Session.Username);

    public string Version => AppInfo.Version;

    public string DatabaseLabel => "Database lokal · skema v" + _databaseInfo.SchemaVersion;

    public event EventHandler? SignOutRequested;

    public event EventHandler? CompanyNameChanged;

    public event EventHandler? WindowStateRequested;

    public event EventHandler? ExitRequested;

    [RelayCommand]
    private void Navigate(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return;
        }

        IsMenuOpen = false;

        if (string.Equals(route, "signout", StringComparison.Ordinal))
        {
            _ = SignOutAsync();
            return;
        }

        if (string.Equals(route, "exit", StringComparison.Ordinal))
        {
            RequestExit();
            return;
        }

        PageTitle = TitleFor(route);

        switch (route)
        {
            case "home":
            case "S02":
                CurrentPage = EnsurePos();
                ActiveRoute = "S02";
                break;

            case "management":
                _management ??= new ManagementViewModel(Navigate);
                CurrentPage = _management;
                ActiveRoute = "management";
                break;

            case "settings":
                if (_settings is null)
                {
                    _settings = new SettingsViewModel(
                        _theme,
                        _preferences,
                        _backups,
                        _databaseOptions,
                        _databaseInfo,
                        _company,
                        _settingsService,
                        _printerService,
                        _aroniumImport,
                        Session.Can(PermissionKeys.SettingsManage));
                    _settings.CloseRequested += () => Navigate("home");
                    _settings.CompanyNameChanged += name =>
                    {
                        _company = _company with { Name = name };
                        OnPropertyChanged(nameof(CompanyName));
                        CompanyNameChanged?.Invoke(this, EventArgs.Empty);
                    };
                }

                CurrentPage = _settings;
                ActiveRoute = "settings";
                _ = _settings.InitializeAsync();
                break;

            case "S03":
                _visualWorkspace ??= new VisualWorkspaceViewModel(_products, _sales, _customers);
                _visualWorkspace.SetSession(Session);
                _visualWorkspace.BackRequested += () => Navigate("home");
                CurrentPage = _visualWorkspace;
                ActiveRoute = "home";
                break;

            case "S04":
                _productSearchPage ??= new ProductSearchViewModel(_products);
                _productSearchPage.ProductSelected += _ => Navigate("home");
                _productSearchPage.CloseRequested += () => Navigate("home");
                CurrentPage = _productSearchPage;
                ActiveRoute = "home";
                break;

            case "S42":
                _unit ??= new UnitViewModel(_units);
                _unit.BackRequested += () => Navigate("home");
                CurrentPage = _unit;
                ActiveRoute = "home";
                break;

            case "S15":
                if (_productList is null)
                {
                    _productList = new ProductListViewModel(_products);
                    _productList.ProductSelected += pid =>
                    {
                        var editor = EnsureProductEditor();
                        editor.SetProduct(pid);
                        Navigate("S16");
                    };
                    _productList.NewProductRequested += () =>
                    {
                        var editor = EnsureProductEditor();
                        editor.NewProduct();
                        Navigate("S16");
                    };
                    _productList.NavigateRequested += Navigate;
                    _productList.BackRequested += () => Navigate("management");
                }

                CurrentPage = _productList;
                ActiveRoute = "S15";
                break;

            case "S16":
                var productEditor = EnsureProductEditor();
                CurrentPage = productEditor;
                ActiveRoute = "S16";
                _ = productEditor.InitializeAsync();
                break;

            case "S41":
                CurrentPage = _productPrice ?? new ProductPriceViewModel(_products, _priceLists);
                ActiveRoute = "home";
                break;

            case "S10":
                _customerSelection ??= new CustomerSelectionViewModel(_customers);
                _customerSelection.BackRequested += () => Navigate("home");
                CurrentPage = _customerSelection;
                ActiveRoute = "home";
                break;

            case "S11":
                if (_receiptViewModelForReprint is not null)
                {
                    CurrentPage = _receiptViewModelForReprint;
                    _receiptViewModelForReprint = null;
                }
                else
                {
                    CurrentPage = new ReceiptViewModel(_sales);
                }
                ActiveRoute = "home";
                break;

            case "S12":
                _openOrders ??= new OpenOrdersViewModel(_sales);
                _openOrders.BackRequested += () => Navigate("home");
                _openOrders.ResumeRequested += resumeData =>
                {
                    EnsurePos().ResumeOpenOrder(resumeData);
                    Navigate("S02");
                };
                CurrentPage = _openOrders;
                ActiveRoute = "home";
                break;

            case "S39":
                _priceList ??= new PriceListViewModel(_priceLists);
                _priceList.ListSelected += id =>
                {
                    _priceListEditor ??= new PriceListEditorViewModel(_priceLists);
                    _priceListEditor.SetPriceList(id);
                    _priceListEditor.BackRequested += () => Navigate("S39");
                    Navigate("S40");
                };
                CurrentPage = _priceList;
                ActiveRoute = "home";
                break;

            case "S40":
                CurrentPage = _priceListEditor ?? new PriceListEditorViewModel(_priceLists);
                ActiveRoute = "home";
                break;

            case "S31":
                _cashMovement ??= new CashMovementViewModel(_cashMovements);
                _cashMovement.BackRequested += () => Navigate("home");
                CurrentPage = _cashMovement;
                ActiveRoute = "home";
                break;

            case "S34":
                _refund ??= new RefundViewModel(_refunds, _sales);
                _refund.BackRequested += () => Navigate("home");
                CurrentPage = _refund;
                ActiveRoute = "home";
                break;

            case "S32":
                _salesHistory ??= new SalesHistoryViewModel(_sales);
                _salesHistory.BackRequested += () => Navigate("home");
                _salesHistory.ViewDetailsRequested += sale =>
                {
                    _receiptViewModelForReprint ??= new ReceiptViewModel(_sales);
                    _receiptViewModelForReprint.SetSale(sale.Id);
                    Navigate("S11");
                };
                CurrentPage = _salesHistory;
                ActiveRoute = "home";
                break;

            case "S35":
                _endOfDayScreen ??= new EndOfDayViewModel(_endOfDay, _sales);
                _endOfDayScreen.BackRequested += () => Navigate("home");
                CurrentPage = _endOfDayScreen;
                ActiveRoute = "home";
                break;

            case "S23":
                _taxRate ??= new TaxRateViewModel(_taxService);
                _taxRate.BackRequested += () => Navigate("home");
                CurrentPage = _taxRate;
                ActiveRoute = "home";
                break;

            case "S24":
                _paymentType ??= new PaymentTypeViewModel(_paymentTypes);
                _paymentType.BackRequested += () => Navigate("home");
                CurrentPage = _paymentType;
                ActiveRoute = "home";
                break;

            case "S25":
                _userManagement ??= new UserManagementViewModel(_users);
                _userManagement.BackRequested += () => Navigate("home");
                CurrentPage = _userManagement;
                ActiveRoute = "home";
                break;

            case "S38":
                _databaseView ??= new DatabaseViewModel(_database, _backups);
                _databaseView.BackRequested += () => Navigate("home");
                CurrentPage = _databaseView;
                ActiveRoute = "home";
                break;

            case "S43":
                _productGroup ??= new ProductGroupViewModel(_productGroupService);
                _productGroup.BackRequested += () => Navigate("home");
                CurrentPage = _productGroup;
                ActiveRoute = "home";
                break;

            case "S44":
                _reportsViewModel ??= new ReportsViewModel(_reportsService);
                _reportsViewModel.BackRequested += () => Navigate("home");
                CurrentPage = _reportsViewModel;
                ActiveRoute = "home";
                break;

            case "S45":
                _stockAdjustment ??= new StockAdjustmentViewModel(_products, _stockAdjustmentService);
                _stockAdjustment.BackRequested += () => Navigate("home");
                CurrentPage = _stockAdjustment;
                ActiveRoute = "home";
                break;

            case "credit":
                CurrentPage = new ScreenStubViewModel(
                    new ScreenDefinition(
                        "S46",
                        "Pembayaran kredit",
                        "Pantau sisa pembayaran kredit pelanggan.",
                        "M9"),
                    () => Navigate("S02"));
                ActiveRoute = "credit";
                break;

            case "feedback":
                CurrentPage = new ScreenStubViewModel(
                    new ScreenDefinition(
                        "S47",
                        "Masukan",
                        "Kirim masukan untuk pengembangan Aroklon.",
                        "M9"),
                    () => Navigate("S02"));
                ActiveRoute = "feedback";
                break;

            default:
                if (ScreenCatalog.TryGet(route, out var screen))
                {
                    CurrentPage = new ScreenStubViewModel(screen, () => Navigate("home"));
                    ActiveRoute = "home";
                }

                break;
        }
    }

    private ProductEditorViewModel EnsureProductEditor()
    {
        if (_productEditor is null)
        {
            _productEditor = new ProductEditorViewModel(
                _products,
                _units,
                _productGroupService);
            _productEditor.BackRequested += () => Navigate("S15");
            _productEditor.PriceViewRequested += pid =>
            {
                _productPrice ??= new ProductPriceViewModel(_products, _priceLists);
                _productPrice.SetProduct(pid);
                _productPrice.BackRequested += () => Navigate("S16");
                Navigate("S41");
            };
        }

        return _productEditor;
    }

    private PosViewModel EnsurePos()
    {
        if (_pos is null)
        {
            _pos = new PosViewModel(_products, _sales, _customers, _cashDrawer);
            _pos.MenuRequested += () => IsMenuOpen = true;
            _pos.RefundRequested += () => Navigate("S34");
            _pos.LockRequested += () => _ = SignOutAsync();
            _pos.OpenOrdersRequested += () => Navigate("S12");
            _pos.SalesHistoryRequested += () => Navigate("S32");
            _pos.BackRequested += () => Navigate("S02");
        }

        _pos.SetSession(Session);
        return _pos;
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        try
        {
            await _auth.LogoutAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Sign out failed");
        }
        finally
        {
            SignOutRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void ToggleMenu() => IsMenuOpen = !IsMenuOpen;

    [RelayCommand]
    private void CloseMenu() => IsMenuOpen = false;

    [RelayCommand]
    private void ToggleWindow() => RequestWindowState();

    [RelayCommand]
    private void Exit() => RequestExit();

    private void RequestWindowState() => WindowStateRequested?.Invoke(this, EventArgs.Empty);

    private void RequestExit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    private static string TitleFor(string route) => route switch
    {
        "home" => "Kasir",
        "S02" => "Kasir",
        "management" => "Manajemen",
        "settings" => "Pengaturan",
        "credit" => "Pembayaran kredit",
        "feedback" => "Masukan",
        _ => ScreenCatalog.TryGet(route, out var screen) ? screen.Title : "Aroklon"
    };

    private static string BuildInitials(string fullName, string username)
    {
        var words = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return username.Length > 0 ? username[..1].ToUpperInvariant() : "?";
        }

        return string.Concat(words.Take(2).Select(word => word[..1].ToUpperInvariant()));
    }
}
