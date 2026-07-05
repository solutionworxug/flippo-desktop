namespace Flippo.Core.Domain;

/// <summary>
/// Port von VocabularyEntry.kt (immutable data class → record).
/// Kotlin-Defaults <c>System.currentTimeMillis()</c> für Zeitstempel werden im pure Core zu 0
/// (deterministisch; das Setzen der Ist-Zeit passiert in der Data-/App-Schicht mit explizitem now).
/// </summary>
public sealed record VocabularyEntry
{
    public long Id { get; init; }
    public long SetId { get; init; }
    public string SourceText { get; init; } = "";
    public string TargetText { get; init; } = "";
    public IReadOnlyList<string> AcceptedAnswers { get; init; } = [];
    public string ExampleSentence { get; init; } = "";
    public string Notes { get; init; } = "";
    public string PartOfSpeech { get; init; } = "";
    public string Gender { get; init; } = "";
    public string PluralForm { get; init; } = "";
    public string VerbForms { get; init; } = "";
    public string Pronunciation { get; init; } = "";
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string Mnemonic { get; init; } = "";
    public string ImagePath { get; init; } = "";
    public string AudioPath { get; init; } = "";
    public int Difficulty { get; init; }          // Kotlin-Default 0
    public int BoxLevel { get; init; } = 1;
    public long NextReviewAt { get; init; }
    public int CorrectCount { get; init; }
    public int WrongCount { get; init; }
    public long LastReviewedAt { get; init; }
    public long CreatedAt { get; init; }
    public long UpdatedAt { get; init; }
    public bool IsArchived { get; init; }
    public bool IsLeech { get; init; }

    /// <summary>
    /// Zuletzt verwendetes Adaptiv-Intervall in Tagen. Bei NULL (Bestandsdaten / Karteikasten):
    /// Fallback-Rekonstruktion aus boxLevel × easeFactor in <see cref="Srs.SrsEngine"/>.
    /// </summary>
    public int? LastIntervalDays { get; init; }

    /// <summary>Kotlin <c>isDue</c> — im pure Core als Methode mit injiziertem now.</summary>
    public bool IsDue(long nowMs) => NextReviewAt <= nowMs && !IsArchived;

    public bool IsNew => CorrectCount == 0 && WrongCount == 0;

    public float SuccessRate
    {
        get
        {
            int total = CorrectCount + WrongCount;
            return total == 0 ? 0f : (float)CorrectCount / total;
        }
    }
}
