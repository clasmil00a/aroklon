using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aroklon.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;

namespace Aroklon.App.ViewModels;

public sealed partial class UserManagementViewModel : ObservableObject
{
    private readonly AdminUserService _userService;

    [ObservableProperty]
    private ObservableCollection<UserDetail> _users = new();

    [ObservableProperty]
    private ObservableCollection<RoleSummary> _roles = new();

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newFullName = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _newRoleId = string.Empty;

    [ObservableProperty]
    private bool _newActive = true;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    private string? _editingId;

    public UserManagementViewModel(AdminUserService users)
    {
        _userService = users;
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
            var list = await _userService.ListAsync();
            Users = new ObservableCollection<UserDetail>(list.Select(u => new UserDetail(
                u.Id, u.Username, u.FullName, u.RoleId, u.RoleName, u.Active, u.LastLoginAt, string.Empty)));
            var roleList = await _userService.ListRolesAsync();
            Roles = new ObservableCollection<RoleSummary>(roleList);
            if (Roles.Count > 0)
            {
                NewRoleId = Roles[0].Id;
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load users");
            ErrorMessage = "Gagal memuat daftar pengguna. Coba lagi.";
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

        if (string.IsNullOrWhiteSpace(NewUsername))
        {
            ErrorMessage = "Nama pengguna wajib diisi.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "Kata sandi wajib diisi.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewRoleId))
        {
            ErrorMessage = "Role wajib dipilih.";
            return;
        }

        IsBusy = true;
        try
        {
            if (_editingId is not null)
            {
                await _userService.UpdateAsync(_editingId, new(
                    string.IsNullOrWhiteSpace(NewFullName) ? null : NewFullName.Trim(),
                    string.IsNullOrWhiteSpace(NewRoleId) ? null : NewRoleId,
                    NewActive,
                    string.IsNullOrWhiteSpace(NewPassword) ? null : NewPassword));
                InfoMessage = "Pengguna diperbarui.";
            }
            else
            {
                await _userService.CreateAsync(new(
                    NewUsername.Trim(),
                    NewFullName.Trim(),
                    NewPassword,
                    NewRoleId,
                    NewActive));
                InfoMessage = "Pengguna dibuat.";
            }

            NewUsername = string.Empty;
            NewFullName = string.Empty;
            NewPassword = string.Empty;
            NewActive = true;
            _editingId = null;
            await InitializeAsync();
        }
        catch (Pos.Domain.ConflictException)
        {
            ErrorMessage = "Nama pengguna sudah dipakai.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to save user");
            ErrorMessage = "Gagal menyimpan pengguna. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Edit(UserDetail? user)
    {
        if (user is null) return;
        _editingId = user.Id;
        NewUsername = user.Username;
        NewFullName = user.FullName;
        NewPassword = string.Empty;
        NewRoleId = user.RoleId;
        NewActive = user.Active;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        _editingId = null;
        NewUsername = string.Empty;
        NewFullName = string.Empty;
        NewPassword = string.Empty;
        NewActive = true;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(UserDetail? user)
    {
        if (user is null || IsBusy) return;
        IsBusy = true;
        try
        {
            await _userService.ToggleActiveAsync(user.Id);
            InfoMessage = user.Active ? "Pengguna dinonaktifkan." : "Pengguna diaktifkan.";
            await InitializeAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to toggle user");
            ErrorMessage = "Gagal mengubah status pengguna. Coba lagi.";
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
