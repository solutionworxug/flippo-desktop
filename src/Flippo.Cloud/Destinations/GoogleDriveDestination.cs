using Flippo.Cloud.Abstractions;

namespace Flippo.Cloud.Destinations;

/// <summary>
/// Backup-Ziel auf Google Drive: alle Backups liegen flach im Ordner „FLIPPO" in „Meine Ablage".
/// <c>RemoteId</c> = Drive-File-Id (kein Pfad → keine Path-Traversal-Fläche). Der Transport läuft
/// über <see cref="IDriveApi"/>; Drive-Fehler werden auf <see cref="DestinationState"/> gemappt.
/// </summary>
public sealed class GoogleDriveDestination : IBackupDestination
{
    /// <summary>Fester Ordnername in „Meine Ablage" (kein Ordner-Picker in diesem Slice).</summary>
    public const string FolderName = "FLIPPO";
    private const string FilePrefix = "flippo-backup-";

    private readonly IDriveApi _api;
    private string? _folderId;

    public GoogleDriveDestination(DestinationConfig config, IDriveApi api)
    {
        DestinationId = config.Id;
        DisplayName = config.DisplayName;
        _api = api;
    }

    public Guid DestinationId { get; }
    public string DisplayName { get; }
    public BackupDestinationKind Kind => BackupDestinationKind.GoogleDrive;

    public async Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken ct = default)
    {
        return await MapAsync(async () =>
        {
            var folderId = await EnsureFolderAsync(ct);
            var files = await _api.ListFilesAsync(folderId, FilePrefix, ct);
            // Zeitstempel im Namen (lexikografisch = chronologisch), absteigend → neuestes zuerst
            // (gleiche Konvention wie LocalFolderDestination und CloudBackupService.PruneAsync).
            IReadOnlyList<BackupFileInfo> result = files
                .Select(f => new BackupFileInfo(f.Id, f.Name, f.CreatedAt, f.SizeBytes))
                .OrderByDescending(b => b.FileName, StringComparer.Ordinal)
                .ToList();
            return result;
        });
    }

    public async Task<BackupFileInfo> UploadAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        return await MapAsync(async () =>
        {
            var folderId = await EnsureFolderAsync(ct);
            var f = await _api.UploadAsync(folderId, fileName, content, ct);
            return new BackupFileInfo(f.Id, f.Name, f.CreatedAt, f.SizeBytes);
        });
    }

    public async Task<Stream> DownloadAsync(string remoteId, CancellationToken ct = default)
        => await MapAsync(() => _api.DownloadAsync(remoteId, ct));

    public async Task DeleteAsync(string remoteId, CancellationToken ct = default)
        => await MapAsync(async () => { await _api.DeleteAsync(remoteId, ct); return true; });

    private async Task<string> EnsureFolderAsync(CancellationToken ct)
        => _folderId ??= await _api.FindOrCreateFolderAsync(FolderName, ct);

    /// <summary>Führt eine Drive-Operation aus und übersetzt <see cref="DriveApiException"/> in eine
    /// <see cref="DestinationException"/> mit UI-Zustand.</summary>
    private static async Task<T> MapAsync<T>(Func<Task<T>> op)
    {
        try
        {
            return await op();
        }
        catch (DriveApiException ex)
        {
            var state = ex.Kind switch
            {
                DriveErrorKind.Unauthorized => DestinationState.NotConnected,
                DriveErrorKind.Timeout => DestinationState.Offline,
                DriveErrorKind.QuotaExceeded => DestinationState.QuotaExceeded,
                _ => DestinationState.TransportFailed
            };
            throw new DestinationException(state, ex.Message, ex);
        }
    }
}
