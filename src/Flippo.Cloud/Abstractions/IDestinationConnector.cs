namespace Flippo.Cloud.Abstractions;

/// <summary>Baut aus einer Config ein Transport-Objekt. Pro Kind eine Implementierung.
/// (Interaktives Verbinden/OAuth wächst mit dem ersten Cloud-Provider-Slice hinzu.)</summary>
public interface IDestinationConnector
{
    BackupDestinationKind Kind { get; }
    IBackupDestination Create(DestinationConfig config);
}
