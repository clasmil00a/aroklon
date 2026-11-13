using Dapper;
using Xunit;

namespace Pos.Data.Tests;

public sealed class BackupTests
{
    [Fact]
    public async Task Daily_backup_creates_a_file_and_a_success_row()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();

        var backup = await db.Backups.CreateDailyAsync();

        Assert.NotNull(backup);
        Assert.Equal("success", backup.Status);
        Assert.True(File.Exists(backup.Path));
        Assert.True(new FileInfo(backup.Path).Length > 0);

        await using var connection = await db.OpenAsync();
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM backup_runs WHERE status = 'success';"));
    }

    [Fact]
    public async Task Second_backup_on_the_same_day_reuses_the_first_one()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();

        var first = await db.Backups.CreateDailyAsync();
        var second = await db.Backups.CreateDailyAsync();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Path, second.Path);

        var files = Directory.GetFiles(db.Options.BackupDirectory, "*.db");
        Assert.Single(files);
    }

    [Fact]
    public async Task Backup_limit_setting_does_not_break_the_job()
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.CreateAdminAsync();
        await db.Settings.SetAsync("backup.retention_days", 7);

        var backup = await db.Backups.CreateDailyAsync();

        Assert.NotNull(backup);
        Assert.Equal("success", backup.Status);
    }
}
