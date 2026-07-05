using Flippo.Core.Domain;

namespace Flippo.Core.Session;

/// <summary>
/// Baut den <see cref="SessionRecord"/> am Session-Ende (Port der Android-Logik, Plan 1.4).
/// Interop-kritisch: der LearningMode-String und das CSV-Format landen im Backup-JSON.
/// </summary>
public static class SessionResult
{
    public static SessionRecord? Build(
        long? setId, string setName, LearningMode mode,
        int correctCount, int wrongCount, IReadOnlyList<long> wrongEntryIds,
        long startedAtMs, long nowMs)
    {
        // Nur schreiben, wenn mindestens eine Karte beantwortet wurde.
        if (correctCount + wrongCount == 0) return null;

        var durationMinutes = (int)Math.Max(1, (nowMs - startedAtMs) / 60_000);

        return new SessionRecord
        {
            SetId = setId,
            SetName = setName,
            LearningMode = ToBackupString(mode),
            CorrectCount = correctCount,
            WrongCount = wrongCount,
            WrongEntryIds = string.Join(",", wrongEntryIds),
            StartedAt = startedAtMs,
            DurationMinutes = durationMinutes
        };
    }

    /// <summary>Domain-Enum → Interop-String (Kotlin-Enum-Namen, Plan 1.3).</summary>
    private static string ToBackupString(LearningMode mode) => mode switch
    {
        LearningMode.Flashcard => "FLASHCARD",
        LearningMode.FreeText => "FREE_TEXT",
        LearningMode.MultipleChoice => "MULTIPLE_CHOICE",
        _ => "FREE_TEXT"
    };
}
