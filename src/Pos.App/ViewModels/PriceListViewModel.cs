using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class PriceListViewModel : ObservableObject
{
    private readonly PriceListService _priceLists;

    [ObservableProperty]
    private ObservableCollection<PriceListSummary> _lists = new();

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public event Action<string>? ListSelected;

    public PriceListViewModel(PriceListService priceLists)
    {
        _priceLists = priceLists;
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
            var list = await _priceLists.ListAsync();
            Lists = new ObservableCollection<PriceListSummary>(list);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load price lists");
            ErrorMessage = "Gagal memuat daftar harga. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectList(PriceListSummary? list)
    {
        if (list is not null)
        {
            ListSelected?.Invoke(list.Id);
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        // Placeholder: M2 UI for creating price lists is minimal; full editor at S40.
        InfoMessage = "Gunakan editor daftar harga (S40) untuk membuat daftar harga baru.";
    }
}
