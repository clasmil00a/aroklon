using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class EndOfDayViewModel : ObservableObject
{
    private readonly EndOfDayService _endOfDay;
    private readonly SaleService _sales;

    [ObservableProperty]
    private string _registerId = "register-1";

    [ObservableProperty]
    private long _closingCash;

    [ObservableProperty]
    private string _varianceNote = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _dayOpen;

    [ObservableProperty]
    private string _businessDate = string.Empty;

    [ObservableProperty]
    private string _expectedCash = string.Empty;

    [ObservableProperty]
    private long _salesCount;

    [ObservableProperty]
    private long _refundCount;

    [ObservableProperty]
    private long _grandTotal;

    [ObservableProperty]
    private long _cashIn;

    [ObservableProperty]
    private long _cashOut;

    private string? _openDayId;

    public EndOfDayViewModel(EndOfDayService endOfDay, SaleService sales)
    {
        _endOfDay = endOfDay;
        _sales = sales;
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
            var summary = await _endOfDay.GetOpenDaySummaryAsync(RegisterId);
            if (summary is null)
            {
                DayOpen = false;
                BusinessDate = string.Empty;
                ExpectedCash = "Tidak ada hari bisnis terbuka.";
                return;
            }

            _openDayId = RegisterId;
            DayOpen = true;
            BusinessDate = summary.BusinessDate;
            ExpectedCash = Pos.Domain.Primitives.MoneyMath.Format(summary.ExpectedCash);
            SalesCount = summary.SalesCount;
            RefundCount = summary.RefundCount;
            GrandTotal = summary.GrandTotal;
            CashIn = summary.CashIn;
            CashOut = summary.CashOut;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load open day summary");
            ErrorMessage = "Gagal memuat ringkasan hari. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CloseDayAsync()
    {
        if (IsBusy || !DayOpen)
        {
            return;
        }

        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        if (ClosingCash < 0)
        {
            ErrorMessage = "Tutup kas tidak boleh negatif.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _endOfDay.CloseAsync(new(
                RegisterId, ClosingCash, string.IsNullOrWhiteSpace(VarianceNote) ? null : VarianceNote.Trim()));

            InfoMessage = $"Hari ditutup. Laporan {result.ReportNumber} · Total {Pos.Domain.Primitives.MoneyMath.Format(result.GrandTotalMinor)}" +
                (result.VarianceMinor != 0 ? $" · Selisih {Pos.Domain.Primitives.MoneyMath.Format(result.VarianceMinor)}" : string.Empty);

            // Refresh to show closed state.
            DayOpen = false;
            BusinessDate = string.Empty;
            ExpectedCash = string.Empty;
            ClosingCash = 0;
        }
        catch (ValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to close day");
            ErrorMessage = "Gagal menutup hari. Coba lagi.";
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
