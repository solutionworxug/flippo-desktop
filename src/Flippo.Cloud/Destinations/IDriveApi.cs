namespace Flippo.Cloud.Destinations;

/// <summary>Eine Datei im Drive-Ordner (roh, transport-neutral).</summary>
public sealed record DriveFile(string Id, string Name, DateTimeOffset CreatedAt, long SizeBytes);

/// <summary>Klassifiziert Drive-Transport-Fehler; wird von <c>GoogleDriveDestination</c> auf
/// <c>DestinationState</c> gemappt.</summary>
public enum DriveErrorKind { Unauthorized, Timeout, QuotaExceeded, Other }

/// <summary>Transport-Fehler der Drive-Ebene (kein UI-Zustand — das macht die Destination).</summary>
public sealed class DriveApiException : Exception
{
    public DriveErrorKind Kind { get; }
    public DriveApiException(DriveErrorKind kind, string message, Exception? inner = null)
        : base(message, inner) => Kind = kind;
}

/// <summary>
/// Dünne Abstraktion über die wenigen Drive-v3-Operationen, die dieses Slice braucht. Der reale
/// Wrapper (<c>DriveApi</c>) kapselt <c>DriveService</c>; Tests nutzen einen In-Memory-Fake, sodass
/// <c>GoogleDriveDestination</c> ohne echtes HTTP/OAuth getestet werden kann.
/// </summary>
public interface IDriveApi
{
    /// <summary>Sucht den Ordner (<c>name=... and mimeType=folder and trashed=false</c>) in „Meine
    /// Ablage" und legt ihn sonst an. Liefert die Ordner-Id.</summary>
    Task<string> FindOrCreateFolderAsync(string name, CancellationToken ct);

    /// <summary>Listet Dateien im Ordner, deren Name <paramref name="nameContains"/> enthält.</summary>
    Task<IReadOnlyList<DriveFile>> ListFilesAsync(string folderId, string nameContains, CancellationToken ct);

    /// <summary>Lädt einen Stream als neue JSON-Datei in den Ordner hoch.</summary>
    Task<DriveFile> UploadAsync(string folderId, string fileName, Stream content, CancellationToken ct);

    /// <summary>Lädt den Dateiinhalt (<c>alt=media</c>) als Stream herunter.</summary>
    Task<Stream> DownloadAsync(string fileId, CancellationToken ct);

    /// <summary>Löscht die Datei.</summary>
    Task DeleteAsync(string fileId, CancellationToken ct);
}
