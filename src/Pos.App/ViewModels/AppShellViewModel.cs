using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Pos.App.Services;
using Pos.Data.Database;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class AppShellViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly ThemeService _theme;
    private readonly UserPreferencesService _preferences;
    private readonly BackupService _backups;
    private readonly DatabaseOptions _databaseOptions;
    private readonly DatabaseInfo _databaseInfo;
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

    private CompanyInfo? _company;

    [ObservableProperty]
    private object? _current;

    [ObservableProperty]
    private string _windowTitle = "POS";

    public AppShellViewModel(
        AuthService auth,
        ThemeService theme,
        UserPreferencesService preferences,
        BackupService backups,
        DatabaseOptions databaseOptions,
        DatabaseInfo databaseInfo,
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
        DatabaseService database)
    {
        _auth = auth;
        _theme = theme;
        _preferences = preferences;
        _backups = backups;
        _databaseOptions = databaseOptions;
        _databaseInfo = databaseInfo;
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
    }

    public async Task InitializeAsync()
    {
        var bootstrap = await _auth.GetBootstrapAsync();
        _company = bootstrap.Company;

        if (bootstrap.Session is not null)
        {
            ShowShell(bootstrap.Session);
            return;
        }

        if (bootstrap.NeedsOnboarding)
        {
            ShowOnboarding();
        }
        else
        {
            ShowLogin();
        }
    }

    private void ShowOnboarding()
    {
        var onboarding = new OnboardingViewModel(
            _auth,
            _company?.Name ?? "Toko Saya",
            AppInfo.Version);

        onboarding.Completed += async session =>
        {
            try
            {
                var bootstrap = await _auth.GetBootstrapAsync();
                _company = bootstrap.Company;
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Could not refresh company after onboarding");
            }

            ShowShell(session);
        };

        WindowTitle = "POS — Siapkan toko";
        Current = onboarding;
    }

    private void ShowLogin()
    {
        var login = new LoginViewModel(
            _auth,
            _company?.Name ?? "Toko Saya",
            AppInfo.Version);

        login.SignedIn += ShowShell;

        WindowTitle = "POS — Masuk";
        Current = login;
    }

    private void ShowShell(UserSession session)
    {
        var shell = new ShellViewModel(
            session,
            _company ?? new CompanyInfo(
                "company", "Toko Saya", null, null, null, null, null,
                "IDR", 0, "id-ID", "Asia/Jakarta", null, null),
            _databaseInfo,
            _auth,
            _theme,
            _preferences,
            _backups,
            _databaseOptions,
            _products,
            _sales,
            _units,
            _priceLists,
            _customers,
            _cashMovements,
            _refunds,
            _endOfDay,
            _paymentTypes,
            _users,
            _database);

        shell.SignOutRequested += (_, _) => ShowLogin();
        shell.FullScreenRequested += (_, _) => ToggleFullScreen();
        shell.ExitRequested += (_, _) => Application.Current.Shutdown();

        WindowTitle = "POS — " + shell.CompanyName;
        Current = shell;
    }

    private static void ToggleFullScreen()
    {
        var window = Application.Current.MainWindow;
        if (window is null)
        {
            return;
        }

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}
