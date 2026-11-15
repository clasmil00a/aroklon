using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Data.Services;
using Pos.Domain;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public LoginViewModel(AuthService auth, string shopName, string version)
    {
        _auth = auth;
        ShopName = shopName;
        Version = version;
    }

    public event Action<UserSession>? SignedIn;

    public string ShopName { get; }

    public string Version { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Isi nama pengguna dan kata sandi.";
            return;
        }

        IsBusy = true;
        try
        {
            var session = await _auth.LoginAsync(Username, Password);
            Password = string.Empty;
            SignedIn?.Invoke(session);
        }
        catch (PosException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Login failed");
            ErrorMessage = "Tidak dapat masuk. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
