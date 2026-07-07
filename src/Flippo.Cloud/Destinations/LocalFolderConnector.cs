using Flippo.Cloud.Abstractions;

namespace Flippo.Cloud.Destinations;

/// <summary>Connector für lokale Ordner. Kein OAuth — die App wählt den Ordner vorab und ruft BuildConfig.</summary>
public sealed class LocalFolderConnector : IDestinationConnector
{
    public const string FolderPathKey = "folderPath";

    public BackupDestinationKind Kind => BackupDestinationKind.LocalFolder;

    public IBackupDestination Create(DestinationConfig config) => new LocalFolderDestination(config);

    public static DestinationConfig BuildConfig(string folderPath, string displayName) => new(
        Guid.NewGuid(), BackupDestinationKind.LocalFolder, displayName,
        new Dictionary<string, string> { [FolderPathKey] = folderPath });
}
