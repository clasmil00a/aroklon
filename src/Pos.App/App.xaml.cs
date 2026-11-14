using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pos.App.Services;
using Pos.App.ViewModels;
using Pos.Data.Database;
using Pos.Data.Security;
using Pos.Data.Services;
using Pos.Domain.Security;
using Pos.Domain.Time;
using Serilog;

namespace Pos.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(
            initiallyOwned: false,
            name: @"Local\PosDesktop.SingleInstance",
            createdNew: out var isFirstInstance);

        if (!isFirstInstance)
        {
            MessageBox.Show(
                "Aplikasi POS sudah berjalan di komputer ini.",
                "POS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        AppPaths.EnsureDirectories();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(AppPaths.LogDirectory, "pos-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            var databaseOptions = new DatabaseOptions(
                AppPaths.DatabasePath,
                AppPaths.BackupDirectory);
            databaseOptions.Validate();

            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(databaseOptions);
                    services.AddSingleton<IDbConnectionFactory>(
                        _ => new SqliteConnectionFactory(databaseOptions.DatabasePath));
                    services.AddSingleton<MigrationRunner>();
                    services.AddSingleton<DatabaseBootstrapper>();
                    services.AddSingleton<IClock, SystemClock>();
                    services.AddSingleton<ISessionStore, SessionStore>();
                    services.AddSingleton(Argon2Options.Default);
                    services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
                    services.AddSingleton<AuthService>();
                    services.AddSingleton<SettingsService>();
                    services.AddSingleton<CompanyService>();
                    services.AddSingleton<AdminUserService>(sp => new AdminUserService(
                        sp.GetRequiredService<IDbConnectionFactory>(),
                        sp.GetRequiredService<ISessionStore>(),
                        sp.GetRequiredService<IPasswordHasher>(),
                        sp.GetRequiredService<IClock>()));
                    services.AddSingleton<BackupService>();
                    services.AddSingleton<ProductService>();
                    services.AddSingleton<SaleService>();
                    services.AddSingleton<UnitService>();
                    services.AddSingleton<PriceListService>();
                    services.AddSingleton<CustomerService>();
                    services.AddSingleton<RegisterService>();
                    services.AddSingleton<CashMovementService>();
                    services.AddSingleton<RefundService>();
                    services.AddSingleton<EndOfDayService>();
                    services.AddSingleton<PaymentTypeService>();
                    services.AddSingleton<DatabaseService>();
                    services.AddSingleton<ProductGroupService>();
                    services.AddSingleton<ReportsService>();
                    services.AddSingleton<ThemeService>();
                    services.AddSingleton(new UserPreferencesService(AppPaths.PreferencesPath));
                })
                .Build();

            await _host.StartAsync();

            var bootstrapper = _host.Services.GetRequiredService<DatabaseBootstrapper>();
            var databaseInfo = await bootstrapper.InitializeAsync();

            var backups = _host.Services.GetRequiredService<BackupService>();
            await backups.TryCreateDailyAsync();

            var preferences = _host.Services.GetRequiredService<UserPreferencesService>();
            var theme = _host.Services.GetRequiredService<ThemeService>();
            theme.Apply(ThemeService.Parse(preferences.Load().Theme));

            var shell = ActivatorUtilities.CreateInstance<AppShellViewModel>(
                _host.Services,
                databaseInfo);
            await shell.InitializeAsync();

            var window = new MainWindow { DataContext = shell };
            MainWindow = window;
            window.Show();

            Log.Information("POS started with database {Path}", databaseInfo.Path);
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Startup failed");
            MessageBox.Show(
                "Aplikasi tidak dapat dimulai.\n\n" + exception.Message,
                "POS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _host?.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Host shutdown failed");
        }

        _host?.Dispose();
        _singleInstance?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled dispatcher exception");
        e.Handled = true;
        MessageBox.Show(
            "Terjadi kesalahan tak terduga. Aplikasi akan ditutup. Data yang sudah tersimpan tetap aman.",
            "POS",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(1);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.ExceptionObject as Exception, "Unhandled application exception");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}
