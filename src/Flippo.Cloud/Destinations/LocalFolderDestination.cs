using Flippo.Cloud.Abstractions;

namespace Flippo.Cloud.Destinations;

/// <summary>Backup-Ziel = ein Ordner im Dateisystem (RemoteId = Dateiname, flach im Ordner).</summary>
public sealed class LocalFolderDestination : IBackupDestination
{
    private const string Prefix = "flippo-backup-";
    private const string SearchPattern = "flippo-backup-*.json";

    private readonly string _folder;

    public LocalFolderDestination(DestinationConfig config)
    {
        DestinationId = config.Id;
        DisplayName = config.DisplayName;
        _folder = config.Settings.TryGetValue(LocalFolderConnector.FolderPathKey, out var p) ? p : "";
    }

    public Guid DestinationId { get; }
    public string DisplayName { get; }
    public BackupDestinationKind Kind => BackupDestinationKind.LocalFolder;

    public Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken ct = default)
    {
        EnsureFolder();
        var list = new List<BackupFileInfo>();
        foreach (var path in Directory.EnumerateFiles(_folder, SearchPattern))
        {
            var fi = new FileInfo(path);
            list.Add(new BackupFileInfo(fi.Name, fi.Name, fi.LastWriteTimeUtc, fi.Length));
        }
        IReadOnlyList<BackupFileInfo> result = list;
        return Task.FromResult(result);
    }

    public async Task<BackupFileInfo> UploadAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        EnsureFolder();
        var path = Path.Combine(_folder, fileName);
        try
        {
            await using (var fs = File.Create(path))
                await content.CopyToAsync(fs, ct);
        }
        catch (IOException ex)
        {
            throw new DestinationException(DestinationState.TransportFailed, ex.Message, ex);
        }
        var fi = new FileInfo(path);
        return new BackupFileInfo(fi.Name, fi.Name, fi.LastWriteTimeUtc, fi.Length);
    }

    public Task<Stream> DownloadAsync(string remoteId, CancellationToken ct = default)
    {
        var path = Path.Combine(_folder, remoteId);
        if (!File.Exists(path))
            throw new DestinationException(DestinationState.TransportFailed, $"Backup '{remoteId}' nicht gefunden.");
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string remoteId, CancellationToken ct = default)
    {
        var path = Path.Combine(_folder, remoteId);
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException ex) { throw new DestinationException(DestinationState.TransportFailed, ex.Message, ex); }
        return Task.CompletedTask;
    }

    private void EnsureFolder()
    {
        if (string.IsNullOrWhiteSpace(_folder) || !Directory.Exists(_folder))
            throw new DestinationException(DestinationState.NotConnected, $"Ordner nicht erreichbar: '{_folder}'.");
    }
}
