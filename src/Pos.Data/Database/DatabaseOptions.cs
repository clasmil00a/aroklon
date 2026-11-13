namespace Pos.Data.Database;

public sealed record DatabaseOptions(string DatabasePath, string BackupDirectory)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DatabasePath))
        {
            throw new ArgumentException("Lokasi database wajib diisi.", nameof(DatabasePath));
        }

        if (string.IsNullOrWhiteSpace(BackupDirectory))
        {
            throw new ArgumentException("Lokasi cadangan wajib diisi.", nameof(BackupDirectory));
        }
    }
}

public sealed record DatabaseInfo(string Path, int SchemaVersion);
