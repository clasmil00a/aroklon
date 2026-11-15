using System.IO;
using System.Text.Json;

namespace Aroklon.App.Services;

public sealed record UserPreferences(string Theme)
{
    public static UserPreferences Default { get; } = new("dark");
}

/// <summary>
/// Small per-Windows-user preferences file. Shop settings live in SQLite;
/// this file only holds choices that must be known before login.
/// </summary>
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
            if (preferences is null ||
                (preferences.Theme != "dark" && preferences.Theme != "light"))
            {
                return UserPreferences.Default;
            }

            return preferences;
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
