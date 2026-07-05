using Flippo.Core.Domain;
using Flippo.Core.Util;

namespace Flippo.Core.Srs;

/// <summary>
/// 1:1-Port von SrsEngine.kt. Zwei Scheduling-Systeme: Karteikasten (Fächer) + Adaptiv (SM-2-ähnlich).
/// <para>Abweichung zum Kotlin-Original (bewusst, Plan 2 / 4.2): <c>now</c> wird als <c>nowMs</c>
/// injiziert statt via <c>System.currentTimeMillis()</c> — deterministisch und testbar.</para>
/// Alle Rundungen nutzen <see cref="JavaCompat.RoundHalfUp"/> (Java-half-up statt .NET-banker's).
/// </summary>
public static class SrsEngine
{
    public const int DefaultLeechThreshold = 4;         // fallback only — use settings.LeechThreshold
    private const int LeechResetStreak = 3;             // GOOD/EASY-Serie zum Aufheben des Leech-Status
    private const long MillisPerDay = 86_400_000L;      // TimeUnit.DAYS.toMillis(1)

    /// <summary>Karteikasten: richtig → nächstes Fach, falsch → Fach 1.</summary>
    public static VocabularyEntryUpdate ScheduleFlashcardBox(
        VocabularyEntry entry,
        ReviewResult result,
        IReadOnlyList<int> boxIntervals,
        long nowMs,
        int leechThreshold = DefaultLeechThreshold)
    {
        bool isCorrect = result.IsCorrect();

        int newBoxLevel = isCorrect
            ? Math.Min(entry.BoxLevel + 1, boxIntervals.Count)
            : 1;

        // Kotlin: boxIntervals.getOrElse(newBoxLevel - 1) { 0 }
        int idx = newBoxLevel - 1;
        int intervalDays = idx >= 0 && idx < boxIntervals.Count ? boxIntervals[idx] : 0;
        long nextReview = nowMs + intervalDays * MillisPerDay;

        int newCorrectCount = isCorrect ? entry.CorrectCount + 1 : entry.CorrectCount;
        int newWrongCount = !isCorrect ? entry.WrongCount + 1 : entry.WrongCount;
        bool isLeechByCount = newWrongCount >= leechThreshold;
        // Reset: 3 Richtige in Folge (newBoxLevel >= 4, da boxLevel bei jedem Falsch auf 1 fällt).
        bool resetLeech = entry.IsLeech && isCorrect && newBoxLevel >= LeechResetStreak + 1;
        bool finalIsLeech = !resetLeech && isLeechByCount;

        return new VocabularyEntryUpdate
        {
            Id = entry.Id,
            BoxLevel = newBoxLevel,
            NextReviewAt = nextReview,
            CorrectCount = newCorrectCount,
            WrongCount = newWrongCount,
            LastReviewedAt = nowMs,
            IsLeech = finalIsLeech,
            LastIntervalDays = intervalDays,
            UpdatedAt = nowMs
            // Difficulty bleibt auf VocabularyEntryUpdate-Default 250 (Karteikasten setzt es nicht).
        };
    }

    /// <summary>Adaptiv (SM-2-ähnlich). difficulty kodiert easeFactor×100; boxLevel = repetitions.</summary>
    public static VocabularyEntryUpdate ScheduleAdaptive(
        VocabularyEntry entry,
        ReviewResult result,
        long nowMs,
        int leechThreshold = DefaultLeechThreshold)
    {
        // < 100 als Sentinel für uninitialisiert (easeFactor < 1.0 unmöglich).
        double easeFactor = entry.Difficulty < 100 ? 2.5 : entry.Difficulty / 100.0;

        // Intervall persistiert; bei NULL (Bestandsdaten): Fallback-Rekonstruktion.
        int interval = entry.LastIntervalDays ?? entry.BoxLevel switch
        {
            0 => 1,
            1 => 1,
            2 => 6,
            _ => (int)JavaCompat.RoundHalfUp(entry.BoxLevel * easeFactor)
        };
        int repetitions = entry.BoxLevel;

        switch (result)
        {
            case ReviewResult.Wrong:
                repetitions = 0;
                interval = 1;
                easeFactor = Math.Max(1.3, easeFactor - 0.2);
                break;
            case ReviewResult.Hard:
                // Schwer: Intervall leicht verlängern, repetitions NICHT erhöhen.
                interval = Math.Max(1, (int)JavaCompat.RoundHalfUp(interval * 1.2));
                easeFactor = Math.Max(1.3, easeFactor - 0.15);
                break;
            case ReviewResult.Good:
                interval = repetitions switch
                {
                    0 => 1,
                    1 => 6,
                    _ => (int)JavaCompat.RoundHalfUp(interval * easeFactor)
                };
                repetitions++;
                break;
            case ReviewResult.Easy:
                interval = (int)JavaCompat.RoundHalfUp(interval * easeFactor * 1.3);
                easeFactor = Math.Min(3.0, easeFactor + 0.15);
                repetitions++;
                break;
        }

        long nextReview = nowMs + (long)interval * MillisPerDay;
        bool isCorrect = result.IsCorrect();
        int newCorrectCount = isCorrect ? entry.CorrectCount + 1 : entry.CorrectCount;
        int newWrongCount = !isCorrect ? entry.WrongCount + 1 : entry.WrongCount;
        bool isLeechByCount = newWrongCount >= leechThreshold;
        // Reset: 3 GOOD/EASY in Folge (repetitions wird bei WRONG auf 0 gesetzt).
        bool resetLeech = entry.IsLeech
            && (result == ReviewResult.Good || result == ReviewResult.Easy)
            && repetitions >= LeechResetStreak;
        bool finalIsLeech = !resetLeech && isLeechByCount;

        return new VocabularyEntryUpdate
        {
            Id = entry.Id,
            BoxLevel = repetitions,
            NextReviewAt = nextReview,
            CorrectCount = newCorrectCount,
            WrongCount = newWrongCount,
            LastReviewedAt = nowMs,
            IsLeech = finalIsLeech,
            Difficulty = (int)JavaCompat.RoundHalfUp(easeFactor * 100),
            LastIntervalDays = interval,
            UpdatedAt = nowMs
        };
    }

    /// <summary>Dispatcher nach <see cref="SrsSettings.Mode"/>.</summary>
    public static VocabularyEntryUpdate Schedule(
        VocabularyEntry entry,
        ReviewResult result,
        SrsSettings settings,
        long nowMs)
        => settings.Mode switch
        {
            SrsMode.FlashcardBox => ScheduleFlashcardBox(entry, result, settings.BoxIntervals, nowMs, settings.LeechThreshold),
            SrsMode.Adaptive => ScheduleAdaptive(entry, result, nowMs, settings.LeechThreshold),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.Mode, "Unbekannter SrsMode")
        };
}
