using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Database;
using Pos.Data.Services;
using Pos.Domain.Models;
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
    private readonly CompanyInfo _company;
    private readonly ProductService _products;
    private readonly SaleService _sales;
    private readonly UnitService _units;
    private readonly PriceListService _priceLists;
    private readonly CustomerService _customers;

    private HomeViewModel? _home;
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

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private string _activeRoute = "home";

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
        CustomerService customers)
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

        NavigationItems =
        [
            new NavigationItemViewModel("home", "Beranda"),
            new NavigationItemViewModel("management", "Manajemen"),
            new NavigationItemViewModel("settings", "Pengaturan")
        ];

        Navigate("home");
    }

    public UserSession Session { get; }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public string CompanyName => _company.Name;

    public string UserDisplayName => Session.FullName;

    public string RoleName => Session.RoleName;

    public string Initials => BuildInitials(Session.FullName, Session.Username);

    public string Version => AppInfo.Version;

    public string DatabaseLabel => "Database lokal · skema v" + _databaseInfo.SchemaVersion;

    public event EventHandler? SignOutRequested;

    public event EventHandler? FullScreenRequested;

    public event EventHandler? ExitRequested;

    [RelayCommand]
    private void Navigate(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return;
        }

        switch (route)
        {
            case "home":
                _home ??= new HomeViewModel(
                    FirstName(Session.FullName),
                    Navigate,
                    RequestFullScreen,
                    SignOutAsync,
                    RequestExit,
                    Version);
                CurrentPage = _home;
                ActiveRoute = "home";
                break;

            case "management":
                _management ??= new ManagementViewModel(Navigate);
                CurrentPage = _management;
                ActiveRoute = "management";
                break;

            case "settings":
                _settings ??= new SettingsViewModel(
                    _theme,
                    _preferences,
                    _backups,
                    _databaseOptions,
                    _databaseInfo,
                    _company);
                CurrentPage = _settings;
                ActiveRoute = "settings";
                break;

            case "S02":
                _pos ??= new PosViewModel(_products, _sales);
                _pos.SetSession(Session);
                _pos.BackRequested += () => Navigate("home");
                CurrentPage = _pos;
                ActiveRoute = "home";
                break;

            case "S42":
                _unit ??= new UnitViewModel(_units);
                _unit.BackRequested += () => Navigate("home");
                CurrentPage = _unit;
                ActiveRoute = "home";
                break;

            case "S15":
                _productList ??= new ProductListViewModel(_products);
                _productList.ProductSelected += pid =>
                {
                    _productEditor ??= new ProductEditorViewModel(_products);
                    _productEditor.SetProduct(pid);
                    Navigate("S16");
                };
                CurrentPage = _productList;
                ActiveRoute = "home";
                break;

            case "S16":
                _productEditor ??= new ProductEditorViewModel(_products);
                _productEditor.BackRequested += () => Navigate("S15");
                _productEditor.PriceViewRequested += pid =>
                {
                    _productPrice ??= new ProductPriceViewModel(_products, _priceLists);
                    _productPrice.SetProduct(pid);
                    _productPrice.BackRequested += () => Navigate("S16");
                    Navigate("S41");
                };
                CurrentPage = _productEditor;
                ActiveRoute = "home";
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

            default:
                if (ScreenCatalog.TryGet(route, out var screen))
                {
                    CurrentPage = new ScreenStubViewModel(screen, () => Navigate("home"));
                    ActiveRoute = "home";
                }

                break;
        }

        UpdateActiveNavigation();
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
    private void FullScreen() => RequestFullScreen();

    [RelayCommand]
    private void Exit() => RequestExit();

    private void RequestFullScreen() => FullScreenRequested?.Invoke(this, EventArgs.Empty);

    private void RequestExit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    private void UpdateActiveNavigation()
    {
        foreach (var item in NavigationItems)
        {
            item.IsActive = string.Equals(item.Route, ActiveRoute, StringComparison.Ordinal);
        }
    }

    private static string FirstName(string fullName)
    {
        var trimmed = fullName.Trim();
        if (trimmed.Length == 0)
        {
            return "Pengguna";
        }

        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
    }

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
