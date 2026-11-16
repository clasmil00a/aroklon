using System.IO;

namespace Pos.App.Services;

/// <summary>
/// Per-user application folders. The install directory is never written to.
/// </summary>
public static class AppPaths
{
    private const string AppFolderName = "Aroklon";
    private const string LegacyFolderName = "PosDesktop";

    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);

    public static string DatabasePath => Path.Combine(DataDirectory, "data", "pos.db");

    public static string BackupDirectory => Path.Combine(DataDirectory, "backups");

    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    public static string PreferencesPath => Path.Combine(DataDirectory, "ui.json");

    public static void EnsureDirectories()
    {
        MigrateLegacyDataFolder();
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    /// <summary>
    /// Moves the old PosDesktop folder to the Aroklon folder so existing
    /// products, sales, and preferences survive the rename. If moving fails,
    /// the database and preferences are copied instead.
    /// </summary>
    private static void MigrateLegacyDataFolder()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        var legacy = Path.Combine(localAppData, LegacyFolderName);
        if (!Directory.Exists(legacy))
        {
            return;
        }

        if (!Directory.Exists(DataDirectory))
        {
            try
            {
                Directory.Move(legacy, DataDirectory);
                return;
            }
            catch (IOException)
            {
                // Fall through to copying the important files.
            }
            catch (UnauthorizedAccessException)
            {
                // Fall through to copying the important files.
            }
        }

        CopyMissingLegacyFiles(legacy);
    }

    private static void CopyMissingLegacyFiles(string legacy)
    {
        try
        {
            var dataDirectory = Path.Combine(DataDirectory, "data");
            Directory.CreateDirectory(dataDirectory);

            var legacyDatabase = Path.Combine(legacy, "data", "pos.db");
            var newDatabase = Path.Combine(dataDirectory, "pos.db");
            if (File.Exists(legacyDatabase) && !File.Exists(newDatabase))
            {
                File.Copy(legacyDatabase, newDatabase, overwrite: false);
            }

            var legacyPreferences = Path.Combine(legacy, "ui.json");
            var newPreferences = Path.Combine(DataDirectory, "ui.json");
            if (File.Exists(legacyPreferences) && !File.Exists(newPreferences))
            {
                File.Copy(legacyPreferences, newPreferences, overwrite: false);
            }
        }
        catch (IOException)
        {
            // A fresh data folder will be created on the next start.
        }
        catch (UnauthorizedAccessException)
        {
            // A fresh data folder will be created on the next start.
        }
    }
}
