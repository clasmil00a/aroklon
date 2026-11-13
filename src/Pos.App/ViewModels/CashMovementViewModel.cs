using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class CashMovementViewModel : ObservableObject
{
    private readonly CashMovementService _cashMovements;

    [ObservableProperty]
    private ObservableCollection<CashMovementSummary> _movements = new();

    [ObservableProperty]
    private string _direction = "in";

    [ObservableProperty]
    private long _amount;

    [ObservableProperty]
    private string _reason = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public CashMovementViewModel(CashMovementService cashMovements)
    {
        _cashMovements = cashMovements;
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var list = await _cashMovements.ListAsync();
            Movements = new ObservableCollection<CashMovementSummary>(list);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load cash movements");
            ErrorMessage = "Gagal memuat gerakan kas. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Direction) || (Direction != "in" && Direction != "out"))
        {
            ErrorMessage = "Arah kas harus Masuk atau Keluar.";
            return;
        }

        if (Amount <= 0)
        {
            ErrorMessage = "Jumlah harus lebih dari nol.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            ErrorMessage = "Alasan wajib diisi.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _cashMovements.CreateAsync(new(
                "register-1", Direction, Amount, Reason.Trim()));

            InfoMessage = Direction == "in"
                ? "Kas masuk dicatat."
                : "Kas keluar dicatat.";

            Direction = "in";
            Amount = 0;
            Reason = string.Empty;
            await LoadAsync();
        }
        catch (ValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to create cash movement");
            ErrorMessage = "Gagal mencatat gerakan kas. Coba lagi.";
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
