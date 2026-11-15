using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class TaxRateViewModel : ObservableObject
{
    private readonly TaxService _taxService;

    [ObservableProperty]
    private ObservableCollection<TaxRateInfo> _taxRates = new();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int _rateBp;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public TaxRateViewModel(TaxService taxService)
    {
        _taxService = taxService;
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var list = await _taxService.ListTaxRatesAsync();
            TaxRates = new ObservableCollection<TaxRateInfo>(list);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load tax rates");
            ErrorMessage = "Gagal memuat tarif pajak. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Nama tarif pajak wajib diisi.";
            return;
        }
        if (RateBp < 0 || RateBp > 100000)
        {
            ErrorMessage = "Tarif harus antara 0 dan 100000 basis poin.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        try
        {
            // Use existing ID if editing, otherwise generate new.
            var id = SelectedTaxRate?.Id ?? Ids.NewId();
            await _taxService.UpsertTaxRateAsync(id, Name, RateBp, IsDefault);

            InfoMessage = "Tarif pajak disimpan.";
            Name = string.Empty;
            RateBp = 0;
            IsDefault = false;
            SelectedTaxRate = null;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to save tax rate");
            ErrorMessage = "Gagal menyimpan tarif pajak. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeactivateAsync(TaxRateInfo? taxRate)
    {
        if (taxRate is null || IsBusy) return;

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await _taxService.DeactivateTaxRateAsync(taxRate.Id);
            InfoMessage = "Tarif pajak dinonaktifkan.";
            SelectedTaxRate = null;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to deactivate tax rate");
            ErrorMessage = "Gagal menonaktifkan tarif pajak. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Edit(TaxRateInfo taxRate)
    {
        Name = taxRate.Name;
        RateBp = taxRate.RateBp;
        IsDefault = taxRate.IsDefault;
        SelectedTaxRate = taxRate;
    }

    [RelayCommand]
    private void Clear()
    {
        Name = string.Empty;
        RateBp = 0;
        IsDefault = false;
        SelectedTaxRate = null;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public event Action? BackRequested;

    private TaxRateInfo? _selectedTaxRate;
    public TaxRateInfo? SelectedTaxRate
    {
        get => _selectedTaxRate;
        set
        {
            if (_selectedTaxRate != value)
            {
                _selectedTaxRate = value;
                if (value is not null)
                {
                    Name = value.Name;
                    RateBp = value.RateBp;
                    IsDefault = value.IsDefault;
                }
                OnPropertyChanged();
            }
        }
    }
}
