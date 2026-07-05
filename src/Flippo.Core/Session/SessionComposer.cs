using Flippo.Core.Domain;

namespace Flippo.Core.Session;

/// <summary>
/// Reine Session-Zusammenstellung. Port von <c>StartLearningSessionUseCase</c>
/// (sortDueFirstNewShuffled + applyLimits) samt filterMode-Selektion, die am Desktop
/// in-memory läuft. Statisch, deterministisch bei injiziertem <paramref name="rng"/> und now.
/// </summary>
public static class SessionComposer
{
    public static SessionPlan Compose(
        IReadOnlyList<VocabularyEntry> candidates,
        SessionComposeOptions options,
        SrsSettings settings,
        Random rng,
        long nowMs)
    {
        // Archivierte immer ausgeschlossen, dann filterMode-Feinselektion.
        var pool = candidates.Where(c => !c.IsArchived);
        var filtered = options.Filter switch
        {
            SessionFilter.Due => pool.Where(c => c.IsDue(nowMs)),
            SessionFilter.New => pool.Where(c => c.IsNew),
            SessionFilter.Leech => pool.Where(c => c.IsLeech),
            _ => pool
        };

        var ordered = SortDueFirstNewShuffled(filtered, rng);
        var limited = ApplyLimits(ordered, options, settings);
        return new SessionPlan { Cards = limited };
    }

    /// <summary>Port von applyLimits: neue-pro-Tag, Box-Filter, Session-Größe (in dieser Reihenfolge).</summary>
    private static List<VocabularyEntry> ApplyLimits(
        List<VocabularyEntry> cards, SessionComposeOptions options, SrsSettings settings)
    {
        IEnumerable<VocabularyEntry> result = cards;

        // Neue Karten pro Tag begrenzen — gezielt NICHT bei Leech- oder Box-Einstieg.
        if (options.Filter != SessionFilter.Leech && options.BoxLevel < 1 && settings.MaxNewCardsPerDay > 0)
        {
            var newCount = 0;
            result = result.Where(entry =>
            {
                if (!entry.IsNew) return true;
                if (newCount >= settings.MaxNewCardsPerDay) return false;
                newCount++;
                return true;
            }).ToList();
        }

        // Box-Filter (gezielter Fach-Einstieg).
        if (options.BoxLevel >= 1)
            result = result.Where(c => c.BoxLevel == options.BoxLevel);

        if (settings.MaxCardsPerSession > 0)
            result = result.Take(settings.MaxCardsPerSession);

        return result.ToList();
    }

    /// <summary>Nicht-neue Karten zuerst (nach NextReviewAt), neue gemischt dahinter (Android sortDueFirstNewShuffled).</summary>
    private static List<VocabularyEntry> SortDueFirstNewShuffled(IEnumerable<VocabularyEntry> cards, Random rng)
    {
        var list = cards.ToList();
        var reviewed = list.Where(c => !c.IsNew).OrderBy(c => c.NextReviewAt);
        var news = list.Where(c => c.IsNew).ToList();
        Shuffle(news, rng);
        return reviewed.Concat(news).ToList();
    }

    /// <summary>In-place Fisher-Yates mit injiziertem RNG (deterministisch für Tests).</summary>
    private static void Shuffle(List<VocabularyEntry> items, Random rng)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
