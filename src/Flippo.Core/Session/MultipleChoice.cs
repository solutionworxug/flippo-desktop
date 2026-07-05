using Flippo.Core.Domain;

namespace Flippo.Core.Session;

/// <summary>
/// Baut die Antwortoptionen für den Multiple-Choice-Modus (Port von generateOptions).
/// Korrekte Antwort + bis zu 3 Distraktoren, alle distinct, am Ende geshuffelt.
/// </summary>
public static class MultipleChoice
{
    /// <param name="sourceToTarget">true = targetText ist die Antwort, sonst sourceText.</param>
    /// <param name="sessionPool">Karten der laufenden Session (primäre Distraktor-Quelle).</param>
    /// <param name="fallbackPool">Gesamt-DB, füllt auf wenn die Session &lt;3 Distraktoren hergibt.</param>
    public static IReadOnlyList<string> BuildOptions(
        VocabularyEntry current, bool sourceToTarget,
        IReadOnlyList<VocabularyEntry> sessionPool,
        IReadOnlyList<VocabularyEntry> fallbackPool,
        Random rng)
    {
        string Text(VocabularyEntry e) => sourceToTarget ? e.TargetText : e.SourceText;
        var correct = Text(current);

        // Distinct, ohne aktuelle Karte, und die richtige Antwort nie als Distraktor.
        var pool = sessionPool
            .Where(e => e.Id != current.Id)
            .Select(Text)
            .Where(t => t != correct)
            .Distinct()
            .ToList();

        List<string> distractors;
        if (pool.Count >= 3)
        {
            distractors = Shuffled(pool, rng).Take(3).ToList();
        }
        else
        {
            // Session gibt zu wenig her → aus der Gesamt-DB auffüllen (ohne Dubletten zum Pool).
            var fallback = fallbackPool
                .Where(e => e.Id != current.Id)
                .Select(Text)
                .Where(t => t != correct)
                .Distinct()
                .Where(t => !pool.Contains(t))
                .ToList();
            distractors = pool.Concat(Shuffled(fallback, rng)).Take(3).ToList();
        }

        return Shuffled([.. distractors, correct], rng);
    }

    private static List<T> Shuffled<T>(IReadOnlyList<T> source, Random rng)
    {
        var list = source.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
