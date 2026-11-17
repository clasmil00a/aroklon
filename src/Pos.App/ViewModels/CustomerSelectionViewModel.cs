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
    private IReadOnlyList<CustomerSummary> _allCustomers = [];

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
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var filtered = string.IsNullOrEmpty(term)
            ? _allCustomers
            : _allCustomers.Where(customer =>
                customer.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (customer.Code?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (customer.PriceListName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        CustomerList = new ObservableCollection<CustomerSummary>(filtered);
    }

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
            _allCustomers = await _customers.ListActiveAsync();
            ApplyFilter();
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
        if (_allCustomers.Count == 0)
        {
            await InitializeAsync();
            return;
        }

        ApplyFilter();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await InitializeAsync();

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public event Action? BackRequested;
}
