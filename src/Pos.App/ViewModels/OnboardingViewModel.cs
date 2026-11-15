using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Data.Services;
using Pos.Domain;
using Pos.Domain.Models;
using Serilog;

namespace Aroklon.App.ViewModels;

public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly AuthService _auth;

    [ObservableProperty]
    private string _shopName;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public OnboardingViewModel(AuthService auth, string shopName, string version)
    {
        _auth = auth;
        _shopName = shopName;
        Version = version;
    }

    public event Action<UserSession>? Completed;

    public string Version { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(ShopName))
        {
            ErrorMessage = "Nama toko wajib diisi.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FullName))
        {
            ErrorMessage = "Nama lengkap wajib diisi.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Nama pengguna wajib diisi.";
            return;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = "Kata sandi minimal 8 karakter.";
            return;
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Konfirmasi kata sandi tidak sama.";
            return;
        }

        IsBusy = true;
        try
        {
            var session = await _auth.CreateFirstAdminAsync(
                ShopName,
                FullName,
                Username,
                Password);

            Password = string.Empty;
            ConfirmPassword = string.Empty;
            Completed?.Invoke(session);
        }
        catch (PosException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Onboarding failed");
            ErrorMessage = "Toko belum dapat dibuat. Periksa data lalu coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
