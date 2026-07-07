namespace Flippo.Cloud.Abstractions;

/// <summary>Art eines Backup-Ziels. FlippoCloud ist reserviert (C3), hier nicht implementiert.</summary>
public enum BackupDestinationKind { LocalFolder, GoogleDrive, OneDrive, FlippoCloud }

/// <summary>UI-Zustand eines Ziels. Offline/QuotaExceeded sind cloud-only (spätere Slices).</summary>
public enum DestinationState { Ready, NotConnected, Offline, QuotaExceeded, TransportFailed }

/// <summary>Ein am Ziel liegendes Backup.</summary>
public sealed record BackupFileInfo(string RemoteId, string FileName, DateTimeOffset CreatedAt, long SizeBytes);

/// <summary>Persistierbare, unsensible Ziel-Konfiguration (LocalFolder: Settings["folderPath"]).</summary>
public sealed record DestinationConfig(
    Guid Id, BackupDestinationKind Kind, string DisplayName, IReadOnlyDictionary<string, string> Settings);

/// <summary>Transport-Fehler mit UI-abbildbarem Zustand.</summary>
public sealed class DestinationException : Exception
{
    public DestinationState State { get; }
    public DestinationException(DestinationState state, string message, Exception? inner = null)
        : base(message, inner) => State = state;
}
