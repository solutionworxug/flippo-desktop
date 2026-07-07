using Flippo.App.Services;
using Flippo.Cloud.Destinations;
using Flippo.Core.Domain;
using Flippo.Data.Services;
using Flippo.Tests.Data;

namespace Flippo.Tests.Cloud;

public class CloudBackupServiceTests
{
    private static (CloudBackupService svc, LocalFolderDestination dest, string dir) Setup(SqliteTestDatabase db)
    {
        var backupsDir = Directory.CreateTempSubdirectory().FullName;
        var svc = new CloudBackupService(new BackupService(db.Factory, backupsDir));
        var dir = Directory.CreateTempSubdirectory().FullName;
        var dest = new LocalFolderDestination(LocalFolderConnector.BuildConfig(dir, "T"));
        return (svc, dest, dir);
    }

    [Fact]
    public async Task Backup_Then_DownloadAndParse_RoundTrips()
    {
        using var db = new SqliteTestDatabase();
        var vocab = new VocabularyStore(db.Factory);
        await vocab.AddSetAsync(new VocabularySet { Title = "Reise", CreatedAt = 1, UpdatedAt = 1 });
        var (svc, dest, _) = Setup(db);

        await svc.BackupToDestinationAsync(dest, null, 1_000, CancellationToken.None);

        var list = await svc.ListBackupsAsync(dest);
        Assert.Single(list);
        var parsed = await svc.DownloadAndParseAsync(dest, list[0].RemoteId);
        Assert.Single(parsed.Content.Sets);
        Assert.Equal("Reise", parsed.Content.Sets[0].Title);
    }

    [Fact]
    public async Task Retention_KeepsLastTen()
    {
        using var db = new SqliteTestDatabase();
        var (svc, dest, _) = Setup(db);

        // 12 Backups, je 1 s auseinander → eindeutige Zeitstempel-Dateinamen
        for (int i = 0; i < 12; i++)
            await svc.BackupToDestinationAsync(dest, null, 1_000 + i * 1_000, CancellationToken.None);

        Assert.Equal(10, (await svc.ListBackupsAsync(dest)).Count);
    }
}
