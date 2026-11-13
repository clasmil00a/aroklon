using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class UnitViewModel : ObservableObject
{
    private readonly UnitService _units;

    [ObservableProperty]
    private ObservableCollection<UnitOption> _unitList = new();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _symbol;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    private string? _editingId;

    public UnitViewModel(UnitService units)
    {
        _units = units;
    }

    public void SetSession() { }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public event Action? BackRequested;

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    [RelayCommand]
    private async Task ExecuteLoadAsync() => await LoadAsync();

    public async Task InitializeAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var list = await _units.ListAsync();
            UnitList = new ObservableCollection<UnitOption>(list);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load units");
            ErrorMessage = "Gagal memuat daftar satuan. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Nama satuan wajib diisi.";
            return;
        }

        if (Name.Trim().Length > 40)
        {
            ErrorMessage = "Nama satuan maksimal 40 karakter.";
            return;
        }

        if (Symbol is not null && Symbol.Trim().Length > 10)
        {
            ErrorMessage = "Simbol satuan maksimal 10 karakter.";
            return;
        }

        IsBusy = true;
        try
        {
            if (_editingId is not null)
            {
                await _units.UpdateAsync(_editingId, new(Name.Trim(), Symbol, true));
                InfoMessage = "Satuan diperbarui.";
            }
            else
            {
                await _units.CreateAsync(new(Name.Trim(), Symbol));
                InfoMessage = "Satuan dibuat.";
            }

            Name = string.Empty;
            Symbol = null;
            _editingId = null;
            await LoadAsync();
        }
        catch (Pos.Domain.ConflictException)
        {
            ErrorMessage = "Nama satuan sudah dipakai.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to save unit");
            ErrorMessage = "Gagal menyimpan satuan. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Edit(UnitOption? unit)
    {
        if (unit is null)
        {
            return;
        }

        _editingId = unit.Id;
        Name = unit.Name;
        Symbol = unit.Symbol;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        _editingId = null;
        Name = string.Empty;
        Symbol = null;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
    }

    public bool IsEditing => _editingId is not null;
}
