using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class SalesHistoryViewModel : ObservableObject
{
    private readonly SaleService _sales;

    [ObservableProperty]
    private ObservableCollection<SaleSummary> _saleList = new();

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private SaleFilter _filter = new();

    public SalesHistoryViewModel(SaleService sales)
    {
        _sales = sales;
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public async Task InitializeAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var list = await _sales.ListAsync(Filter);
            SaleList = new ObservableCollection<SaleSummary>(list);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load sales history");
            ErrorMessage = "Gagal memuat riwayat penjualan. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await InitializeAsync();
    }

    [RelayCommand]
    private async Task ViewDetailsAsync(SaleSummary? sale)
    {
        if (sale is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var detail = await _sales.GetAsync(sale.Id);
            ViewDetailsRequested?.Invoke(detail);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load sale details");
            ErrorMessage = "Gagal memuat detail transaksi. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public event Action? BackRequested;
    public event Action<SaleDetail>? ViewDetailsRequested;
}
