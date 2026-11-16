using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain;
using Pos.Domain.Models;
using Serilog;

namespace Pos.App.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly UserPreferencesService _preferences;
    private bool _isInitializing;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _rememberUsername;

    public LoginViewModel(
        AuthService auth,
        UserPreferencesService preferences,
        string shopName,
        string version)
    {
        _auth = auth;
        _preferences = preferences;
        ShopName = shopName;
        Version = version;

        var saved = preferences.Load();
        _isInitializing = true;
        RememberUsername = saved.RememberUsername;
        Username = saved.RememberUsername ? saved.LastUsername ?? string.Empty : string.Empty;
        _isInitializing = false;
    }

    public event Action<UserSession>? SignedIn;

    public event Action? ExitRequested;

    public string ShopName { get; }

    public string Version { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    partial void OnRememberUsernameChanged(bool value)
    {
        if (_isInitializing)
        {
            return;
        }

        var current = _preferences.Load();
        _preferences.Save(current with
        {
            RememberUsername = value,
            LastUsername = value
                ? (string.IsNullOrWhiteSpace(Username) ? current.LastUsername : Username.Trim())
                : null
        });
    }

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke();

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

            var current = _preferences.Load();
            _preferences.Save(current with
            {
                RememberUsername = RememberUsername,
                LastUsername = RememberUsername ? session.Username : null
            });

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
