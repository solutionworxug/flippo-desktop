namespace Flippo.Cloud.Abstractions;

/// <summary>
/// Erweitert <see cref="IDestinationConnector"/> um interaktives Verbinden (OAuth im System-Browser).
/// LocalFolder bleibt auf der Basis (Ordner-Pick ist App-UI); Cloud-Provider implementieren dies.
/// </summary>
public interface IInteractiveConnector : IDestinationConnector
{
    /// <summary>Startet den interaktiven Verbinde-Flow und liefert eine persistierbare Config.
    /// <c>null</c> = vom Nutzer abgebrochen.</summary>
    Task<DestinationConfig?> ConnectInteractiveAsync(CancellationToken ct = default);
}
