namespace Flippo.Cloud.Abstractions;

/// <summary>Transport für Backup-Dateien (Stream rein/raus). Auth ist orthogonal (Connector).</summary>
public interface IBackupDestination
{
    Guid DestinationId { get; }
    string DisplayName { get; }
    BackupDestinationKind Kind { get; }

    Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken ct = default);
    Task<BackupFileInfo> UploadAsync(string fileName, Stream content, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string remoteId, CancellationToken ct = default);
    Task DeleteAsync(string remoteId, CancellationToken ct = default);
}
