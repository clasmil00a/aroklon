using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class PaymentTypeViewModel : ObservableObject
{
    private readonly PaymentTypeService _paymentTypes;

    [ObservableProperty]
    private ObservableCollection<PaymentTypeSummary> _types = new();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _icon;

    [ObservableProperty]
    private string? _shortcut;

    [ObservableProperty]
    private bool _markAsPaid = true;

    [ObservableProperty]
    private bool _opensCashDrawer;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private int _sortOrder;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    private string? _editingId;

    public PaymentTypeViewModel(PaymentTypeService paymentTypes)
    {
        _paymentTypes = paymentTypes;
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public async Task InitializeAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var list = await _paymentTypes.ListAsync();
            Types = new ObservableCollection<PaymentTypeSummary>(list);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load payment types");
            ErrorMessage = "Gagal memuat tipe pembayaran. Coba lagi.";
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
            ErrorMessage = "Nama tipe pembayaran wajib diisi.";
            return;
        }

        if (Name.Trim().Length > 60)
        {
            ErrorMessage = "Nama tipe pembayaran maksimal 60 karakter.";
            return;
        }

        IsBusy = true;
        try
        {
            if (_editingId is not null)
            {
                await _paymentTypes.UpdateAsync(_editingId, new(
                    Name.Trim(), Icon, Shortcut, MarkAsPaid, IsDefault, OpensCashDrawer, true, SortOrder));
                InfoMessage = "Tipe pembayaran diperbarui.";
            }
            else
            {
                await _paymentTypes.CreateAsync(new(
                    Name.Trim(), Icon, Shortcut, MarkAsPaid, IsDefault, OpensCashDrawer, SortOrder));
                InfoMessage = "Tipe pembayaran dibuat.";
            }

            Name = string.Empty;
            Icon = null;
            Shortcut = null;
            MarkAsPaid = true;
            OpensCashDrawer = false;
            SortOrder = 0;
            _editingId = null;
            await InitializeAsync();
        }
        catch (Pos.Domain.ConflictException)
        {
            ErrorMessage = "Nama tipe pembayaran sudah dipakai.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to save payment type");
            ErrorMessage = "Gagal menyimpan tipe pembayaran. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Edit(PaymentTypeSummary? type)
    {
        if (type is null) return;
        _editingId = type.Id;
        Name = type.Name;
        Icon = type.Icon;
        Shortcut = type.Shortcut;
        MarkAsPaid = type.MarkAsPaid;
        IsDefault = type.IsDefault;
        OpensCashDrawer = type.OpensCashDrawer;
        SortOrder = type.SortOrder;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        _editingId = null;
        Name = string.Empty;
        Icon = null;
        Shortcut = null;
        MarkAsPaid = true;
        IsDefault = false;
        OpensCashDrawer = false;
        SortOrder = 0;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(PaymentTypeSummary? type)
    {
        if (type is null || IsBusy) return;

        IsBusy = true;
        try
        {
            await _paymentTypes.UpdateAsync(type.Id, new(
                null, null, null, null, null, null, !type.Active, null));
            InfoMessage = type.Active ? "Tipe pembayaran dinonaktifkan." : "Tipe pembayaran diaktifkan.";
            await InitializeAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to toggle payment type");
            ErrorMessage = "Gagal mengubah status tipe pembayaran. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await InitializeAsync();

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public event Action? BackRequested;
}
