namespace Flippo.Cloud.Abstractions;

/// <summary>
/// Verschlüsselte Ablage sensibler Strings (v.a. OAuth-Refresh-Tokens), ein Blob je Schlüssel.
/// Windows-DPAPI-Impl in diesem Slice; Interface offen für spätere Plattformen (C1 Nicht-Ziel).
/// </summary>
public interface ITokenVault
{
    /// <summary>Speichert (oder ersetzt) das Geheimnis unter <paramref name="key"/>.</summary>
    void Store(string key, string secret);

    /// <summary>Liest das Geheimnis; <c>null</c>, wenn nicht vorhanden oder unlesbar.</summary>
    string? Retrieve(string key);

    /// <summary>Löscht das Geheimnis (idempotent — kein Fehler, wenn nicht vorhanden).</summary>
    void Delete(string key);
}
