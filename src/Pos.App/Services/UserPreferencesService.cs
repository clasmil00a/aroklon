using System.IO;
using System.Text.Json;

namespace Pos.App.Services;

/// <summary>
/// Preferences that must be known before a user signs in. Shop settings live
/// in SQLite; this file only stores per-Windows-user UI choices.
/// </summary>
public sealed record UserPreferences
{
    public string Theme { get; init; } = "dark";

    public bool RememberUsername { get; init; }

    public string? LastUsername { get; init; }

    public bool StartFullScreen { get; init; } = true;

    public static UserPreferences Default { get; } = new();
}

public sealed class UserPreferencesService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public UserPreferencesService(string path)
    {
        _path = path;
    }

    public UserPreferences Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return UserPreferences.Default;
            }

            var json = File.ReadAllText(_path);
            var preferences = JsonSerializer.Deserialize<UserPreferences>(json, SerializerOptions);
            if (preferences is null)
            {
                return UserPreferences.Default;
            }

            var theme = preferences.Theme is "dark" or "light"
                ? preferences.Theme
                : UserPreferences.Default.Theme;
            var lastUsername = string.IsNullOrWhiteSpace(preferences.LastUsername)
                ? null
                : preferences.LastUsername.Trim();

            return preferences with
            {
                Theme = theme,
                LastUsername = lastUsername
            };
        }
        catch (IOException)
        {
            return UserPreferences.Default;
        }
        catch (JsonException)
        {
            return UserPreferences.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return UserPreferences.Default;
        }
    }

    public void Save(UserPreferences preferences)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(preferences, SerializerOptions));
        }
        catch (IOException)
        {
            // A read-only profile must not stop a sale.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
