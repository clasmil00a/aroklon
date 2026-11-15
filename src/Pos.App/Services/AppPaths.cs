using System.IO;

namespace Aroklon.App.Services;

/// <summary>
/// Per-user application folders. The install directory is never written to.
/// </summary>
public static class AppPaths
{
    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PosDesktop");

    public static string DatabasePath => Path.Combine(DataDirectory, "data", "pos.db");

    public static string BackupDirectory => Path.Combine(DataDirectory, "backups");

    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    public static string PreferencesPath => Path.Combine(DataDirectory, "ui.json");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
