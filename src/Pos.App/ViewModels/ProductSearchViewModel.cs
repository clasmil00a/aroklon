using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

/// <summary>
/// Advanced product search modal (F3). Searches by name or product code,
/// with keyboard navigation (arrow keys to select, Enter to confirm).
/// </summary>
public sealed partial class ProductSearchViewModel : ObservableObject
{
    private readonly ProductService _products;

    [ObservableProperty]
    private ObservableCollection<ProductLookup> _results = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ProductSearchViewModel(ProductService products)
    {
        _products = products;
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    public ProductLookup? SelectedResult =>
        SelectedIndex >= 0 && SelectedIndex < Results.Count
            ? Results[SelectedIndex]
            : null;

    partial void OnSearchTextChanged(string value)
    {
        _ = SearchAsync();
    }

    public async Task InitializeAsync()
    {
        SelectedIndex = -1;
        SearchText = string.Empty;
        Results = new ObservableCollection<ProductLookup>();
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        SelectedIndex = -1;

        try
        {
            var list = await _products.SearchAsync(SearchText, 50);
            Results = new ObservableCollection<ProductLookup>(list);
            if (Results.Count > 0)
            {
                SelectedIndex = 0;
            }
            InfoMessage = $"{Results.Count} produk ditemukan.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Advanced product search failed");
            ErrorMessage = "Pencarian produk gagal. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (Results.Count == 0) return;
        SelectedIndex = SelectedIndex <= 0 ? Results.Count - 1 : SelectedIndex - 1;
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (Results.Count == 0) return;
        SelectedIndex = SelectedIndex >= Results.Count - 1 ? 0 : SelectedIndex + 1;
    }

    [RelayCommand]
    private void SelectCurrent() => ProductSelected?.Invoke(SelectedResult);

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    public event Action<ProductLookup>? ProductSelected;
    public event Action? CloseRequested;
}
