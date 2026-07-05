using Flippo.Core.Domain;

namespace Flippo.Core.Session;

/// <summary>
/// Feinselektion der Kandidaten (Android <c>filterMode</c>). Am Desktop in-memory statt DB-Query:
/// der Store liefert die breite Kandidaten-Menge, der <see cref="SessionComposer"/> filtert.
/// </summary>
public enum SessionFilter
{
    /// <summary>Nur fällige Karten (<c>NextReviewAt &lt;= now</c>). Standard-Einstieg.</summary>
    Due,
    /// <summary>Alle Karten des Scopes (ohne archivierte).</summary>
    All,
    /// <summary>Nur neue Karten (noch nie beantwortet).</summary>
    New,
    /// <summary>Nur Leech-Karten.</summary>
    Leech
}

/// <summary>Steuerparameter für <see cref="SessionComposer.Compose"/> (Port der Android-SessionParams, reduziert).</summary>
public sealed record SessionComposeOptions
{
    public SessionFilter Filter { get; init; } = SessionFilter.Due;

    /// <summary>0 = kein Box-Filter; &gt;=1 = nur Karten dieser Box (gezielter Fach-Einstieg).</summary>
    public int BoxLevel { get; init; }
}

/// <summary>Ergebnis der Zusammenstellung: die geordnete, limitierte Kartenliste der Session.</summary>
public sealed record SessionPlan
{
    public IReadOnlyList<VocabularyEntry> Cards { get; init; } = [];
}
