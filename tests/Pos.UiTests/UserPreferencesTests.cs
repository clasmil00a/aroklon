using System;
using System.IO;
using Pos.App.Services;
using Xunit;

namespace Pos.UiTests;

public sealed class UserPreferencesTests
{
    [Fact]
    public void Legacy_theme_only_file_keeps_new_defaults()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "ui.json");
            File.WriteAllText(path, "{\"Theme\":\"light\"}");

            var preferences = new UserPreferencesService(path).Load();

            Assert.Equal("light", preferences.Theme);
            Assert.False(preferences.RememberUsername);
            Assert.Null(preferences.LastUsername);
            Assert.True(preferences.StartFullScreen);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Saved_preferences_round_trip()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "ui.json");
            var service = new UserPreferencesService(path);
            service.Save(new UserPreferences
            {
                Theme = "light",
                RememberUsername = true,
                LastUsername = "kasir",
                StartFullScreen = false
            });

            var preferences = service.Load();

            Assert.Equal("light", preferences.Theme);
            Assert.True(preferences.RememberUsername);
            Assert.Equal("kasir", preferences.LastUsername);
            Assert.False(preferences.StartFullScreen);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Invalid_theme_falls_back_to_dark()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "ui.json");
            File.WriteAllText(path, "{\"Theme\":\"purple\"}");

            var preferences = new UserPreferencesService(path).Load();

            Assert.Equal("dark", preferences.Theme);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "pos-ui-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
