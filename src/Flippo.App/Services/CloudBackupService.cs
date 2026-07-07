using Flippo.Cloud.Abstractions;
using Flippo.Core.Backup;
using Flippo.Core.Domain;
using Flippo.Data.Services;

namespace Flippo.App.Services;

/// <summary>
/// Verbindet den (Stream-basierten) BackupService mit einem IBackupDestination: Sichern (mit
/// Retention), Auflisten, Download+Parse. UI-frei — Preview/Import bleibt im SetActionsService.
/// </summary>
public sealed class CloudBackupService
{
    public const int KeepBackups = 10;

    private readonly BackupService _backup;

    public CloudBackupService(BackupService backup) => _backup = backup;

    public async Task<BackupFileInfo> BackupToDestinationAsync(
        IBackupDestination dest, SrsSettings? srs, long nowMs, CancellationToken ct = default)
    {
        using var mem = new MemoryStream();
        await _backup.ExportAsync(mem, srs, nowMs, ct);
        mem.Position = 0;

        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(nowMs).UtcDateTime.ToString("yyyyMMdd-HHmmss");
        var info = await dest.UploadAsync($"flippo-backup-{timestamp}.json", mem, ct);

        await PruneAsync(dest, ct);
        return info;
    }

    public Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(IBackupDestination dest, CancellationToken ct = default)
        => dest.ListBackupsAsync(ct);

    public async Task<BackupParseResult> DownloadAndParseAsync(
        IBackupDestination dest, string remoteId, CancellationToken ct = default)
    {
        await using var stream = await dest.DownloadAsync(remoteId, ct);
        return await _backup.ParseAsync(stream, ct);
    }

    /// <summary>Behält die neuesten <see cref="KeepBackups"/>; Zeitstempel steckt im Dateinamen
    /// (lexikografisch = chronologisch), daher nach FileName absteigend sortieren.</summary>
    private async Task PruneAsync(IBackupDestination dest, CancellationToken ct)
    {
        var stale = (await dest.ListBackupsAsync(ct))
            .OrderByDescending(b => b.FileName, StringComparer.Ordinal)
            .Skip(KeepBackups)
            .ToList();
        foreach (var b in stale)
            await dest.DeleteAsync(b.RemoteId, ct);
    }
}
