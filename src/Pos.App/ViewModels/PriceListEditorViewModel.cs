using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aroklon.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Aroklon.App.ViewModels;

public sealed partial class PriceListEditorViewModel : ObservableObject
{
    private readonly PriceListService _priceLists;

    [ObservableProperty]
    private PriceListSummary? _list;

    [ObservableProperty]
    private ObservableCollection<PriceListItemView> _items = new();

    [ObservableProperty]
    private string _newPriceText = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private bool _editApplyPromotions;

    [ObservableProperty]
    private bool _isEditFormVisible;

    private string _priceListId = string.Empty;

    public PriceListEditorViewModel(PriceListService priceLists)
    {
        _priceLists = priceLists;
    }

    public void SetPriceList(string priceListId) => _priceListId = priceListId;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public async Task InitializeAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(_priceListId))
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var list = await _priceLists.ListAsync();
            List = list.FirstOrDefault(l => l.Id == _priceListId);
            var items = await _priceLists.GetItemsAsync(_priceListId);
            Items = new ObservableCollection<PriceListItemView>(items);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load price list items");
            ErrorMessage = "Gagal memuat item daftar harga. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveItemPriceAsync(PriceListItemView? item)
    {
        if (item is null || IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(NewPriceText))
        {
            ErrorMessage = "Harga wajib diisi.";
            return;
        }

        if (!long.TryParse(NewPriceText.Replace(".", "").Replace(",", ""), out var price))
        {
            ErrorMessage = "Harga tidak valid.";
            return;
        }

        if (price < 0)
        {
            ErrorMessage = "Harga tidak boleh negatif.";
            return;
        }

        IsBusy = true;
        try
        {
            await _priceLists.SetItemPriceAsync(_priceListId, item.ProductUnitId,
                new PriceListItemUpdateRequest(price, null));
            InfoMessage = "Harga diperbarui.";
            NewPriceText = string.Empty;
            await InitializeAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to save price");
            ErrorMessage = "Gagal menyimpan harga. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearItemPriceAsync(PriceListItemView? item)
    {
        if (item is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _priceLists.SetItemPriceAsync(_priceListId, item.ProductUnitId,
                new PriceListItemUpdateRequest(null, null));
            InfoMessage = "Harga kembali ke warisan.";
            await InitializeAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to clear price");
            ErrorMessage = "Gagal menghapus harga. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ShowEditForm()
    {
        if (IsBusy || List is null) return;

        EditName = List.Name;
        EditApplyPromotions = List.ApplyPromotions;
        IsEditFormVisible = true;
    }

    [RelayCommand]
    private void HideEditForm()
    {
        IsEditFormVisible = false;
        EditName = string.Empty;
        EditApplyPromotions = false;
    }

    [RelayCommand]
    private async Task UpdateListAsync()
    {
        if (List is null || IsBusy) return;

        if (string.IsNullOrWhiteSpace(EditName))
        {
            ErrorMessage = "Nama daftar harga wajib diisi.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await _priceLists.UpdateAsync(List.Id, new(
                EditName.Trim(),
                EditApplyPromotions,
                List.Active
            ));

            InfoMessage = "Daftar harga diperbarui.";
            IsEditFormVisible = false;
            await InitializeAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to update price list");
            ErrorMessage = "Gagal memperbarui daftar harga. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleListActiveAsync()
    {
        if (List is null || IsBusy) return;

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await _priceLists.ToggleActiveAsync(List.Id, !List.Active);

            InfoMessage = List.Active ? "Daftar harga dinonaktifkan." : "Daftar harga diaktifkan.";
            await InitializeAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to toggle price list");
            ErrorMessage = "Gagal mengubah status daftar harga. Coba lagi.";
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
