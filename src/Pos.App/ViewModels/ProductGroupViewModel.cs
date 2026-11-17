using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class ProductGroupViewModel : ObservableObject
{
    private readonly ProductGroupService _productGroups;

    [ObservableProperty]
    private ObservableCollection<ProductGroup> _groups = new();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private int _sortOrder;

    [ObservableProperty]
    private bool _active = true;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    private string? _editingId;

    public ProductGroupViewModel(ProductGroupService productGroups)
    {
        _productGroups = productGroups;
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;
    public bool IsEditing => _editingId is not null;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    [RelayCommand]
    private async Task RefreshAsync() => await InitializeAsync();

    public async Task InitializeAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var list = await _productGroups.ListAsync();
            Groups = new ObservableCollection<ProductGroup>(list);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load product groups");
            ErrorMessage = "Gagal memuat daftar grup produk. Coba lagi.";
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
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Nama grup produk wajib diisi.";
            return;
        }

        if (Name.Trim().Length > 60)
        {
            ErrorMessage = "Nama grup produk maksimal 60 karakter.";
            return;
        }

        IsBusy = true;
        try
        {
            if (_editingId is not null)
            {
                await _productGroups.UpdateAsync(_editingId, new(
                    Name.Trim(), Description, SortOrder, Active));
                InfoMessage = "Grup produk diperbarui.";
            }
            else
            {
                await _productGroups.CreateAsync(new(
                    Name.Trim(), Description, SortOrder, Active));
                InfoMessage = "Grup produk dibuat.";
            }

            Name = string.Empty;
            Description = null;
            SortOrder = 0;
            Active = true;
            _editingId = null;
            OnPropertyChanged(nameof(IsEditing));
            await InitializeAsync();
        }
        catch (Pos.Domain.ConflictException)
        {
            ErrorMessage = "Nama grup produk sudah dipakai.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to save product group");
            ErrorMessage = "Gagal menyimpan grup produk. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Edit(ProductGroup? group)
    {
        if (group is null) return;
        _editingId = group.Id;
        OnPropertyChanged(nameof(IsEditing));
        Name = group.Name;
        Description = group.Description;
        SortOrder = group.SortOrder;
        Active = group.Active;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        _editingId = null;
        OnPropertyChanged(nameof(IsEditing));
        Name = string.Empty;
        Description = null;
        SortOrder = 0;
        Active = true;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(ProductGroup? group)
    {
        if (group is null || IsBusy) return;
        IsBusy = true;
        try
        {
            await _productGroups.UpdateAsync(group.Id, new(
                null, null, null, !group.Active));
            InfoMessage = group.Active ? "Grup produk dinonaktifkan." : "Grup produk diaktifkan.";
            await InitializeAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to toggle product group");
            ErrorMessage = "Gagal mengubah status grup produk. Coba lagi.";
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
