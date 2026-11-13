using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class CustomerSelectionViewModel : ObservableObject
{
    private readonly CustomerService _customers;

    [ObservableProperty]
    private ObservableCollection<CustomerSummary> _customerList = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public event Action<string>? CustomerSelected;

    public CustomerSelectionViewModel(CustomerService customers)
    {
        _customers = customers;
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
            var list = await _customers.ListActiveAsync();
            CustomerList = new ObservableCollection<CustomerSummary>(list);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load customers");
            ErrorMessage = "Gagal memuat daftar pelanggan. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectCustomer(CustomerSummary? customer)
    {
        if (customer is not null)
        {
            CustomerSelected?.Invoke(customer.Id);
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await InitializeAsync();
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public event Action? BackRequested;
}
