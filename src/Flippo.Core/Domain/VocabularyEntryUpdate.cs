namespace Flippo.Core.Domain;

/// <summary>
/// Port von VocabularyEntryUpdate (SrsEngine.kt). Ergebnis eines Reviews.
/// <c>Difficulty</c>-Default 250 ist bindend: der Karteikasten-Pfad setzt difficulty NICHT,
/// die Karte behält also easeFactor 2.5 — exakt wie die Kotlin data class.
/// </summary>
public sealed record VocabularyEntryUpdate
{
    public long Id { get; init; }
    public int BoxLevel { get; init; }
    public long NextReviewAt { get; init; }
    public int CorrectCount { get; init; }
    public int WrongCount { get; init; }
    public long LastReviewedAt { get; init; }
    public bool IsLeech { get; init; }
    public int Difficulty { get; init; } = 250;
    public int? LastIntervalDays { get; init; }
    public long UpdatedAt { get; init; }
}
