using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Database;
using Pos.Data.Services;
using Pos.Domain;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ThemeService _theme;
    private readonly UserPreferencesService _preferences;
    private readonly BackupService _backups;
    private readonly DatabaseOptions _databaseOptions;
    private readonly DatabaseInfo _databaseInfo;
    private readonly SettingsService _settings;

    private bool _isDarkTheme;
    private bool _isLightTheme;

    [ObservableProperty]
    private string _backupMessage = string.Empty;

    [ObservableProperty]
    private bool _isBackingUp;

    [ObservableProperty]
    private string _databaseSize = string.Empty;

    private bool _negativeStockBlock = true;

    public bool NegativeStockBlock
    {
        get => _negativeStockBlock;
        set
        {
            if (_negativeStockBlock != value)
            {
                _negativeStockBlock = value;
                OnPropertyChanged(nameof(NegativeStockBlock));
                _ = _settings.SetAsync("pos.negative_stock", value ? "block" : "allow");
            }
        }
    }

    public SettingsViewModel(
        ThemeService theme,
        UserPreferencesService preferences,
        BackupService backups,
        DatabaseOptions databaseOptions,
        DatabaseInfo databaseInfo,
        CompanyInfo company,
        SettingsService settings)
    {
        _theme = theme;
        _preferences = preferences;
        _backups = backups;
        _databaseOptions = databaseOptions;
        _databaseInfo = databaseInfo;
        _settings = settings;

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

        var negativeStockJson = _settings.GetAsync("pos.negative_stock").Result;
        _negativeStockBlock = string.IsNullOrWhiteSpace(negativeStockJson) || negativeStockJson != "allow";

        var preferencesValue = preferences.Load();
        if (preferencesValue.Theme == "light")
        {
            IsLightTheme = true;
        }
        else
        {
            IsDarkTheme = true;
        }
    }

    public string ShopName { get; }

    public string CurrencyLabel { get; }

    public string Timezone { get; }

    public string LocaleLabel { get; }

    public string DatabasePath { get; }

    public string BackupDirectory { get; }

    public string DataDirectory { get; }

    public string SchemaVersionText { get; }

    public string AppVersion { get; }

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

    partial void OnBackupMessageChanged(string value) => OnPropertyChanged(nameof(HasBackupMessage));

    partial void OnIsBackingUpChanged(bool value) => OnPropertyChanged(nameof(IsNotBackingUp));

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

    private void ApplyTheme(bool light)
    {
        _theme.Apply(light ? AppTheme.Light : AppTheme.Dark);
        _preferences.Save(new UserPreferences(light ? "light" : "dark"));

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
