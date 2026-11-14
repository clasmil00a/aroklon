using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class OpenOrdersViewModel : ObservableObject
{
    private readonly SaleService _sales;

    [ObservableProperty]
    private ObservableCollection<OpenSaleSummary> _orders = new();

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public OpenOrdersViewModel(SaleService sales)
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
            var orders = await _sales.ListOpenAsync();
            Orders = new ObservableCollection<OpenSaleSummary>(orders);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load open orders");
            ErrorMessage = "Gagal memuat pesanan terbuka. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResumeAsync(OpenSaleSummary? order)
    {
        if (order is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var resumeData = await _sales.GetOpenSaleForResumeAsync(order.Id);
            ResumeRequested?.Invoke(resumeData);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to resume open order");
            ErrorMessage = "Gagal melanjutkan pesanan. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public event Action? BackRequested;
    public event Action<OpenOrderResumeData>? ResumeRequested;
}
