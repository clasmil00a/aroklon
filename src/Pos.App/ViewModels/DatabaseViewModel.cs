using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aroklon.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Aroklon.App.ViewModels;

public sealed partial class DatabaseViewModel : ObservableObject
{
    private readonly DatabaseService _database;
    private readonly BackupService _backupService;

    [ObservableProperty]
    private HealthCheckResult _health = new(false, 0, false, false, null);

    [ObservableProperty]
    private ObservableCollection<BackupRunSummary> _backups = new();

    [ObservableProperty]
    private string _restorePath = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private BackupScheduleSetting _schedule = new(false, 2, 0);

    public DatabaseViewModel(DatabaseService database, BackupService backups)
    {
        _database = database;
        _backupService = backups;
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public async Task InitializeAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            _health = await _database.CheckHealthAsync();
            var list = await _database.ListBackupsAsync();
            Backups = new ObservableCollection<BackupRunSummary>(list);
            _schedule = await _database.GetBackupScheduleAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load database status");
            ErrorMessage = "Gagal memuat status database. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await InitializeAsync();
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(RestorePath)) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        try
        {
            await _database.RestoreAsync(new(RestorePath));
            InfoMessage = "Database berhasil dipulihkan. Silakan restart aplikasi.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to restore database");
            ErrorMessage = "Pemulihan gagal. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveScheduleAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        try
        {
            await _database.SetBackupScheduleAsync(_schedule);
            InfoMessage = "Jadwal cadangan disimpan.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to save backup schedule");
            ErrorMessage = "Gagal menyimpan jadwal cadangan. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public event Action? BackRequested;
}
