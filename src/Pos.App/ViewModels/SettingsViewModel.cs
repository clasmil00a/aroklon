using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Pos.App.Services;
using Pos.Data.Database;
using Pos.Data.Services;
using Pos.Domain;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed record SettingsCategoryInfo(string Key, string Title, string Icon);

public sealed partial class SettingsViewModel : ObservableObject
{
    private static readonly string[] StoreSettingKeys =
    [
        "pos.negative_stock",
        "pos.cash_drawer_enabled",
        "pos.discount_enabled",
        "pos.allow_price_override",
        "pos.require_void_reason",
        "pos.credit_payments_enabled",
        "receipt.show_units",
        "receipt.print_enabled",
        "receipt.printer_name",
        "receipt.header",
        "receipt.footer"
    ];

    private readonly ThemeService _theme;
    private readonly UserPreferencesService _preferences;
    private readonly BackupService _backups;
    private readonly DatabaseOptions _databaseOptions;
    private readonly DatabaseInfo _databaseInfo;
    private readonly SettingsService _settings;
    private readonly PrinterService _printers;
    private readonly AroniumImportService _aroniumImport;

    private bool _isDarkTheme;
    private bool _isLightTheme;
    private bool _suppressSave;
    private bool _initialized;

    [ObservableProperty]
    private string _backupMessage = string.Empty;

    [ObservableProperty]
    private bool _isBackingUp;

    [ObservableProperty]
    private string _databaseSize = string.Empty;

    [ObservableProperty]
    private string _shopName = string.Empty;

    [ObservableProperty]
    private string _settingsMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _selectedCategory = "general";

    [ObservableProperty]
    private bool _negativeStockBlock = true;

    [ObservableProperty]
    private bool _cashDrawerEnabled = true;

    [ObservableProperty]
    private bool _discountEnabled = true;

    [ObservableProperty]
    private bool _allowPriceOverride;

    [ObservableProperty]
    private bool _requireVoidReason = true;

    [ObservableProperty]
    private bool _creditPaymentsEnabled;

    [ObservableProperty]
    private bool _showReceiptUnits = true;

    [ObservableProperty]
    private bool _printReceiptEnabled;

    [ObservableProperty]
    private string? _selectedPrinter;

    [ObservableProperty]
    private string _receiptHeader = string.Empty;

    [ObservableProperty]
    private string _receiptFooter = string.Empty;

    [ObservableProperty]
    private bool _rememberUsername;

    [ObservableProperty]
    private bool _startFullScreen = true;

    [ObservableProperty]
    private string _aroniumPath = string.Empty;

    [ObservableProperty]
    private string _aroniumMessage = string.Empty;

    [ObservableProperty]
    private bool _isAroniumBusy;

    [ObservableProperty]
    private AroniumImportPreview? _aroniumPreview;

    [ObservableProperty]
    private bool _aroniumImportCompany = true;

    [ObservableProperty]
    private bool _aroniumImportCustomers = true;

    [ObservableProperty]
    private bool _aroniumImportPaymentTypes = true;

    [ObservableProperty]
    private bool _aroniumImportStock;

    [ObservableProperty]
    private bool _aroniumUpdateExisting = true;

    [ObservableProperty]
    private string _aroniumResultDetails = string.Empty;

    public SettingsViewModel(
        ThemeService theme,
        UserPreferencesService preferences,
        BackupService backups,
        DatabaseOptions databaseOptions,
        DatabaseInfo databaseInfo,
        CompanyInfo company,
        SettingsService settings,
        PrinterService printers,
        AroniumImportService aroniumImport,
        bool canManageSettings)
    {
        _theme = theme;
        _preferences = preferences;
        _backups = backups;
        _databaseOptions = databaseOptions;
        _databaseInfo = databaseInfo;
        _settings = settings;
        _printers = printers;
        _aroniumImport = aroniumImport;
        CanManageSettings = canManageSettings;

        Categories =
        [
            new("general", "Umum", "\uE713"),
            new("order", "Pesanan & pembayaran", "\uE8A1"),
            new("print", "Cetak & struk", "\uE749"),
            new("database", "Database", "\uE8B7"),
            new("about", "Tentang", "\uE946")
        ];

        ShopName = company.Name;
        CurrencyLabel = company.Currency == "IDR"
            ? "Rupiah (IDR)"
            : company.Currency + " (" + company.Currency + ")";
        Timezone = company.Timezone;
        LocaleLabel = "Bahasa Indonesia (" + company.Locale + ")";
        DatabasePath = databaseInfo.Path;
        BackupDirectory = databaseOptions.BackupDirectory;
        DataDirectory = AppPaths.DataDirectory;
        SchemaVersionText = "Versi " + databaseInfo.SchemaVersion;
        AppVersion = AppInfo.Version;
        RefreshDatabaseSize();
        LoadPrinters();

        var preferencesValue = preferences.Load();
        _suppressSave = true;
        RememberUsername = preferencesValue.RememberUsername;
        StartFullScreen = preferencesValue.StartFullScreen;
        _suppressSave = false;

        if (preferencesValue.Theme == "light")
        {
            _isLightTheme = true;
        }
        else
        {
            _isDarkTheme = true;
        }
    }

    public IReadOnlyList<SettingsCategoryInfo> Categories { get; }

    public ObservableCollection<string> Printers { get; } = [];

    public bool CanManageSettings { get; }

    public bool CanEditSettings => CanManageSettings && !IsLoading;

    public bool CannotManageSettings => !CanManageSettings;

    public bool HasPrinters => Printers.Count > 0;

    public bool HasNoPrinters => !HasPrinters;

    public bool HasAroniumPath => !string.IsNullOrWhiteSpace(AroniumPath);

    public bool HasAroniumMessage => !string.IsNullOrWhiteSpace(AroniumMessage);

    public bool HasAroniumPreview => AroniumPreview is not null;

    public bool IsNotAroniumBusy => !IsAroniumBusy;

    public bool CanInspectAronium =>
        CanManageSettings && !IsAroniumBusy && HasAroniumPath;

    public bool CanImportAronium =>
        CanManageSettings && !IsAroniumBusy && AroniumPreview?.CanImport == true;

    public bool HasAroniumWarnings => !string.IsNullOrWhiteSpace(AroniumWarningsText);

    public bool HasAroniumResultDetails =>
        !string.IsNullOrWhiteSpace(AroniumResultDetails);

    public string AroniumFileInfo
    {
        get
        {
            if (AroniumPreview is null)
            {
                return string.Empty;
            }

            var size = AroniumPreview.FileSizeBytes switch
            {
                >= 1024L * 1024L => string.Format(
                    "{0:0.#} MB",
                    AroniumPreview.FileSizeBytes / (1024d * 1024d)),
                >= 1024 => string.Format("{0:0.#} KB", AroniumPreview.FileSizeBytes / 1024d),
                _ => AroniumPreview.FileSizeBytes + " byte"
            };
            return AroniumPreview.FileName + " · " + size;
        }
    }

    public string AroniumCountsText
    {
        get
        {
            if (AroniumPreview is null)
            {
                return string.Empty;
            }

            return string.Join(" · ",
                AroniumPreview.ProductCount.ToString("N0") + " produk",
                AroniumPreview.GroupCount.ToString("N0") + " grup",
                AroniumPreview.BarcodeCount.ToString("N0") + " barcode",
                AroniumPreview.TaxCount.ToString("N0") + " pajak",
                AroniumPreview.CustomerCount.ToString("N0") + " pelanggan",
                AroniumPreview.SupplierCount.ToString("N0") + " pemasok",
                AroniumPreview.PaymentTypeCount.ToString("N0") + " pembayaran",
                AroniumPreview.StockProductCount.ToString("N0") + " produk berstok");
        }
    }

    public string AroniumWarningsText
    {
        get
        {
            var warnings = AroniumPreview?.Warnings ?? [];
            return warnings.Count == 0
                ? string.Empty
                : "• " + string.Join("\n• ", warnings);
        }
    }

    public string CurrencyLabel { get; }

    public string Timezone { get; }

    public string LocaleLabel { get; }

    public string DatabasePath { get; }

    public string BackupDirectory { get; }

    public string DataDirectory { get; }

    public string SchemaVersionText { get; }

    public string AppVersion { get; }

    public bool IsGeneralCategory => SelectedCategory == "general";

    public bool IsOrderCategory => SelectedCategory == "order";

    public bool IsPrintCategory => SelectedCategory == "print";

    public bool IsDatabaseCategory => SelectedCategory == "database";

    public bool IsAboutCategory => SelectedCategory == "about";

    public string SelectedCategoryTitle => SelectedCategory switch
    {
        "order" => "Pesanan & pembayaran",
        "print" => "Cetak & struk",
        "database" => "Database",
        "about" => "Tentang aplikasi",
        _ => "Umum"
    };

    public string SelectedCategoryDescription => SelectedCategory switch
    {
        "order" => "Atur perilaku stok, diskon, pembayaran, dan laci kas.",
        "print" => "Pilih printer Windows, aktifkan cetak struk, dan atur isi struk.",
        "database" => "Lokasi database, cadangan, pemulihan, dan impor data dari Aronium Lite.",
        "about" => "Versi aplikasi dan informasi teknis lainnya.",
        _ => "Tema, preferensi saat aplikasi dibuka, serta bahasa dan format."
    };

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value) && value)
            {
                ApplyTheme(light: false);
            }
        }
    }

    public bool IsLightTheme
    {
        get => _isLightTheme;
        set
        {
            if (SetProperty(ref _isLightTheme, value) && value)
            {
                ApplyTheme(light: true);
            }
        }
    }

    public bool HasBackupMessage => !string.IsNullOrWhiteSpace(BackupMessage);

    public bool IsNotBackingUp => !IsBackingUp;

    public bool HasSettingsMessage => !string.IsNullOrWhiteSpace(SettingsMessage);

    public event Action? CloseRequested;

    public event Action<string>? CompanyNameChanged;

    partial void OnBackupMessageChanged(string value) => OnPropertyChanged(nameof(HasBackupMessage));

    partial void OnIsBackingUpChanged(bool value) => OnPropertyChanged(nameof(IsNotBackingUp));

    partial void OnSettingsMessageChanged(string value) => OnPropertyChanged(nameof(HasSettingsMessage));

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(CanEditSettings));

    partial void OnAroniumPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasAroniumPath));
        OnPropertyChanged(nameof(CanInspectAronium));
    }

    partial void OnAroniumMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasAroniumMessage));

    partial void OnIsAroniumBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotAroniumBusy));
        OnPropertyChanged(nameof(CanInspectAronium));
        OnPropertyChanged(nameof(CanImportAronium));
    }

    partial void OnAroniumPreviewChanged(AroniumImportPreview? value)
    {
        OnPropertyChanged(nameof(HasAroniumPreview));
        OnPropertyChanged(nameof(CanImportAronium));
        OnPropertyChanged(nameof(AroniumFileInfo));
        OnPropertyChanged(nameof(AroniumCountsText));
        OnPropertyChanged(nameof(AroniumWarningsText));
        OnPropertyChanged(nameof(HasAroniumWarnings));
    }

    partial void OnAroniumResultDetailsChanged(string value) =>
        OnPropertyChanged(nameof(HasAroniumResultDetails));

    partial void OnSelectedCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(IsGeneralCategory));
        OnPropertyChanged(nameof(IsOrderCategory));
        OnPropertyChanged(nameof(IsPrintCategory));
        OnPropertyChanged(nameof(IsDatabaseCategory));
        OnPropertyChanged(nameof(IsAboutCategory));
        OnPropertyChanged(nameof(SelectedCategoryTitle));
        OnPropertyChanged(nameof(SelectedCategoryDescription));
    }

    partial void OnNegativeStockBlockChanged(bool value) =>
        SaveSetting("pos.negative_stock", value ? "block" : "allow");

    partial void OnCashDrawerEnabledChanged(bool value) =>
        SaveSetting("pos.cash_drawer_enabled", value);

    partial void OnDiscountEnabledChanged(bool value) =>
        SaveSetting("pos.discount_enabled", value);

    partial void OnAllowPriceOverrideChanged(bool value) =>
        SaveSetting("pos.allow_price_override", value);

    partial void OnRequireVoidReasonChanged(bool value) =>
        SaveSetting("pos.require_void_reason", value);

    partial void OnCreditPaymentsEnabledChanged(bool value) =>
        SaveSetting("pos.credit_payments_enabled", value);

    partial void OnShowReceiptUnitsChanged(bool value) =>
        SaveSetting("receipt.show_units", value);

    partial void OnPrintReceiptEnabledChanged(bool value) =>
        SaveSetting("receipt.print_enabled", value);

    partial void OnSelectedPrinterChanged(string? value) =>
        SaveSetting("receipt.printer_name", value ?? string.Empty);

    partial void OnRememberUsernameChanged(bool value)
    {
        if (_suppressSave)
        {
            return;
        }

        try
        {
            var current = _preferences.Load();
            _preferences.Save(current with
            {
                RememberUsername = value,
                LastUsername = value ? current.LastUsername : null
            });
            SettingsMessage = "Preferensi disimpan.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not save username preference");
            SettingsMessage = "Preferensi gagal disimpan.";
        }
    }

    partial void OnStartFullScreenChanged(bool value)
    {
        if (_suppressSave)
        {
            return;
        }

        try
        {
            _preferences.Save(_preferences.Load() with { StartFullScreen = value });
            SettingsMessage = "Preferensi disimpan.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not save maximized window preference");
            SettingsMessage = "Preferensi gagal disimpan.";
        }
    }

    /// <summary>
    /// Loads shop settings once. It is deliberately asynchronous: blocking the
    /// UI thread on SQLite was one reason the settings page felt unresponsive.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        try
        {
            var values = await _settings.GetManyAsync(StoreSettingKeys).ConfigureAwait(true);
            _suppressSave = true;

            NegativeStockBlock = !string.Equals(
                Read(values, "pos.negative_stock"),
                "allow",
                StringComparison.OrdinalIgnoreCase);
            CashDrawerEnabled = ReadBoolean(values, "pos.cash_drawer_enabled", defaultValue: true);
            DiscountEnabled = ReadBoolean(values, "pos.discount_enabled", defaultValue: true);
            AllowPriceOverride = ReadBoolean(values, "pos.allow_price_override", defaultValue: false);
            RequireVoidReason = ReadBoolean(values, "pos.require_void_reason", defaultValue: true);
            CreditPaymentsEnabled = ReadBoolean(values, "pos.credit_payments_enabled", defaultValue: false);
            ShowReceiptUnits = ReadBoolean(values, "receipt.show_units", defaultValue: true);
            PrintReceiptEnabled = ReadBoolean(values, "receipt.print_enabled", defaultValue: false);
            ReceiptHeader = Read(values, "receipt.header") ?? string.Empty;
            ReceiptFooter = Read(values, "receipt.footer") ?? string.Empty;

            var printer = Read(values, "receipt.printer_name");
            if (!string.IsNullOrWhiteSpace(printer) &&
                !Printers.Any(name => string.Equals(name, printer, StringComparison.OrdinalIgnoreCase)))
            {
                Printers.Add(printer);
                OnPropertyChanged(nameof(HasPrinters));
                OnPropertyChanged(nameof(HasNoPrinters));
            }

            SelectedPrinter = string.IsNullOrWhiteSpace(printer) ? null : printer;
        }
        catch (PosException exception)
        {
            SettingsMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not load shop settings");
            SettingsMessage = "Pengaturan toko tidak dapat dimuat. Coba buka kembali halaman ini.";
        }
        finally
        {
            _suppressSave = false;
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void RefreshPrinters()
    {
        LoadPrinters();
        SettingsMessage = HasPrinters
            ? "Daftar printer diperbarui."
            : "Tidak ada printer Windows terdeteksi. Pasang printer di Windows terlebih dahulu.";
    }

    [RelayCommand]
    private void TestPrint()
    {
        try
        {
            var target = string.IsNullOrWhiteSpace(SelectedPrinter)
                ? "printer bawaan Windows"
                : SelectedPrinter;
            _printers.PrintText(
                SelectedPrinter,
                "Uji printer",
                "Aroklon POS\nUji printer berhasil.\n" +
                DateTime.Now.ToString("dd/MM/yyyy HH:mm") +
                "\n\nTerima kasih.");
            SettingsMessage = "Halaman uji dikirim ke " + target + ".";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Test print failed");
            SettingsMessage = "Cetak uji gagal. Periksa printer dan driver Windows.";
        }
    }

    [RelayCommand]
    private async Task SaveReceiptSettingsAsync()
    {
        if (!CanManageSettings)
        {
            SettingsMessage = "Anda tidak memiliki izin untuk mengubah pengaturan.";
            return;
        }

        try
        {
            await _settings.UpdateManyAsync(new Dictionary<string, object?>
            {
                ["receipt.header"] = ReceiptHeader.Trim(),
                ["receipt.footer"] = ReceiptFooter.Trim()
            });
            SettingsMessage = "Pengaturan struk disimpan.";
        }
        catch (PosException exception)
        {
            SettingsMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not save receipt settings");
            SettingsMessage = "Pengaturan struk gagal disimpan. Coba lagi.";
        }
    }

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        if (IsBackingUp)
        {
            return;
        }

        IsBackingUp = true;
        BackupMessage = "Membuat cadangan database...";
        try
        {
            var run = await _backups.CreateDailyAsync();
            BackupMessage = "Cadangan siap: " + run.Path;
            RefreshDatabaseSize();
        }
        catch (PosException exception)
        {
            BackupMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Manual backup failed");
            BackupMessage = "Cadangan gagal dibuat. Periksa folder cadangan.";
        }
        finally
        {
            IsBackingUp = false;
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DataDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Could not open the data folder");
            BackupMessage = "Folder data tidak dapat dibuka.";
        }
    }

    [RelayCommand]
    private async Task BrowseAroniumDatabaseAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pilih database Aronium Lite (pos.db)",
            Filter =
                "Database Aronium (*.db;*.db3;*.sqlite;*.sqlite3;*.bak)|*.db;*.db3;*.sqlite;*.sqlite3;*.bak|" +
                "Semua file (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        AroniumPath = dialog.FileName;
        AroniumPreview = null;
        AroniumResultDetails = string.Empty;
        await InspectAroniumCoreAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task InspectAroniumAsync()
    {
        if (!CanInspectAronium)
        {
            if (!HasAroniumPath)
            {
                AroniumMessage = "Pilih file pos.db Aronium terlebih dahulu.";
            }

            return;
        }

        await InspectAroniumCoreAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ImportAroniumAsync()
    {
        if (!CanImportAronium || AroniumPreview is null)
        {
            AroniumMessage = "Periksa file Aronium terlebih dahulu sebelum mengimpor.";
            return;
        }

        var confirmation = System.Windows.MessageBox.Show(
            $"Impor data dari \"{AroniumPreview.FileName}\"?\n\n" +
            $"{AroniumPreview.ProductCount:N0} produk, {AroniumPreview.CustomerCount:N0} pelanggan, " +
            $"dan {AroniumPreview.PaymentTypeCount:N0} tipe pembayaran akan digabungkan.\n\n" +
            "Data yang sudah ada tidak dihapus. Produk dengan SKU atau barcode yang sama " +
            "akan diperbarui bila opsi \"Perbarui data yang sudah ada\" dicentang.",
            "Impor database Aronium",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question,
            System.Windows.MessageBoxResult.No);
        if (confirmation != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        IsAroniumBusy = true;
        AroniumMessage = "Mengimpor data Aronium...";
        AroniumResultDetails = string.Empty;
        try
        {
            var result = await _aroniumImport.ImportAsync(new AroniumImportOptions(
                AroniumPath,
                ImportCompany: AroniumImportCompany,
                ImportCustomers: AroniumImportCustomers,
                ImportPaymentTypes: AroniumImportPaymentTypes,
                ImportStock: AroniumImportStock,
                UpdateExisting: AroniumUpdateExisting)).ConfigureAwait(true);

            var parts = new List<string>();
            if (result.ProductsCreated > 0)
            {
                parts.Add(result.ProductsCreated + " produk baru");
            }

            if (result.ProductsUpdated > 0)
            {
                parts.Add(result.ProductsUpdated + " produk diperbarui");
            }

            if (result.GroupsCreated > 0)
            {
                parts.Add(result.GroupsCreated + " grup");
            }

            if (result.BarcodesCreated > 0)
            {
                parts.Add(result.BarcodesCreated + " barcode");
            }

            if (result.TaxesCreated > 0)
            {
                parts.Add(result.TaxesCreated + " pajak");
            }

            if (result.CustomersCreated > 0 || result.CustomersUpdated > 0)
            {
                parts.Add(
                    (result.CustomersCreated + result.CustomersUpdated) + " pelanggan/pemasok");
            }

            if (result.PaymentTypesCreated > 0 || result.PaymentTypesUpdated > 0)
            {
                parts.Add(
                    (result.PaymentTypesCreated + result.PaymentTypesUpdated) + " tipe pembayaran");
            }

            if (result.StockMovementsCreated > 0)
            {
                parts.Add(result.StockMovementsCreated + " stok awal");
            }

            AroniumMessage = parts.Count == 0
                ? "Impor selesai. Tidak ada data baru yang perlu ditambahkan."
                : "Impor selesai: " + string.Join(", ", parts) + ".";

            if (result.CompanyUpdated)
            {
                if (!string.IsNullOrWhiteSpace(result.CompanyName))
                {
                    ShopName = result.CompanyName;
                    CompanyNameChanged?.Invoke(result.CompanyName);
                }

                AroniumMessage += " Profil toko diperbarui.";
            }

            var details = new List<string>();
            if (result.ProductsSkipped > 0)
            {
                details.Add(result.ProductsSkipped + " produk dilewati karena sudah ada.");
            }

            if (result.StockProductsSkipped > 0)
            {
                details.Add(
                    result.StockProductsSkipped +
                    " stok produk dilewati karena produk sudah memiliki pergerakan stok.");
            }

            details.AddRange(result.Warnings);
            AroniumResultDetails = details.Count == 0
                ? string.Empty
                : "• " + string.Join("\n• ", details);

            RefreshDatabaseSize();
        }
        catch (PosException exception)
        {
            AroniumMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Aronium import failed");
            AroniumMessage = "Impor Aronium gagal. Tidak ada data yang diubah; periksa file lalu coba lagi.";
        }
        finally
        {
            IsAroniumBusy = false;
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    private async Task InspectAroniumCoreAsync()
    {
        IsAroniumBusy = true;
        AroniumMessage = "Memeriksa database Aronium...";
        AroniumResultDetails = string.Empty;
        try
        {
            var preview = await _aroniumImport.InspectAsync(AroniumPath).ConfigureAwait(true);
            AroniumPreview = preview;
            AroniumMessage = preview.CanImport
                ? "Pratinjau siap. Periksa data di bawah, lalu klik Impor."
                : "Database Aronium tidak berisi produk yang dapat diimpor.";
        }
        catch (PosException exception)
        {
            AroniumPreview = null;
            AroniumMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not inspect Aronium database");
            AroniumPreview = null;
            AroniumMessage =
                "Database Aronium tidak dapat dibaca. Tutup Aronium, salin file pos.db ke folder lain, lalu coba lagi.";
        }
        finally
        {
            IsAroniumBusy = false;
        }
    }

    private void LoadPrinters()
    {
        var selected = SelectedPrinter;
        Printers.Clear();
        foreach (var printer in _printers.GetPrinterNames())
        {
            Printers.Add(printer);
        }

        if (!string.IsNullOrWhiteSpace(selected) &&
            !Printers.Any(name => string.Equals(name, selected, StringComparison.OrdinalIgnoreCase)))
        {
            Printers.Add(selected);
        }

        SelectedPrinter = selected;
        OnPropertyChanged(nameof(HasPrinters));
        OnPropertyChanged(nameof(HasNoPrinters));
    }

    private static string? Read(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static bool ReadBoolean(
        IReadOnlyDictionary<string, string> values,
        string key,
        bool defaultValue)
    {
        var value = Read(values, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private void SaveSetting(string key, object value)
    {
        if (_suppressSave || !CanManageSettings)
        {
            return;
        }

        _ = SaveSettingAsync(key, value);
    }

    private async Task SaveSettingAsync(string key, object value)
    {
        try
        {
            await _settings.SetAsync(key, value);
            SettingsMessage = "Pengaturan disimpan.";
        }
        catch (PosException exception)
        {
            SettingsMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not save setting {Key}", key);
            SettingsMessage = "Pengaturan gagal disimpan. Coba lagi.";
        }
    }

    private void ApplyTheme(bool light)
    {
        _theme.Apply(light ? AppTheme.Light : AppTheme.Dark);

        var preferences = _preferences.Load();
        _preferences.Save(preferences with { Theme = light ? "light" : "dark" });

        if (light)
        {
            _isDarkTheme = false;
        }
        else
        {
            _isLightTheme = false;
        }

        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsLightTheme));
    }

    private void RefreshDatabaseSize()
    {
        try
        {
            DatabaseSize = File.Exists(_databaseInfo.Path)
                ? string.Format("{0:0.#} KB", new FileInfo(_databaseInfo.Path).Length / 1024d)
                : "Belum dibuat";
        }
        catch (IOException)
        {
            DatabaseSize = "Tidak diketahui";
        }
    }
}
