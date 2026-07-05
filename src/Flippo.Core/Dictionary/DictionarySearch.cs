using System.Text;
using System.Text.RegularExpressions;
using Flippo.Core.Domain;

namespace Flippo.Core.Dictionary;

/// <summary>
/// Pure Wörterbuch-Suche (Port von UserDictionaryRepositoryImpl.searchEntries): akzent-insensitiv
/// über Quell- UND Zielwort, gerankt (exakt → Präfix → enthält), begrenzt. Diakritika-Strip wie
/// FreeTextChecker: NFD + Combining-Diacritical-Marks-Block (nicht Kategorie Mn).
/// </summary>
public static class DictionarySearch
{
    private static readonly Regex CombiningMarks =
        new(@"\p{IsCombiningDiacriticalMarks}+", RegexOptions.Compiled);

    public static IReadOnlyList<UserDictionaryEntry> Filter(
        IReadOnlyList<UserDictionaryEntry> entries, string query, int limit = 100)
    {
        var q = Normalize(query);
        if (q.Length == 0)
            return entries.Count <= limit ? entries : entries.Take(limit).ToList();

        var ranked = new List<(UserDictionaryEntry Entry, int Rank)>();
        foreach (var e in entries)
        {
            int r = Rank(e, q);
            if (r >= 0) ranked.Add((e, r));
        }

        return ranked
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Entry.SourceWord, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => x.Entry)
            .ToList();
    }

    private static int Rank(UserDictionaryEntry e, string normalizedQuery)
    {
        int rs = RankField(Normalize(e.SourceWord), normalizedQuery);
        int rt = RankField(Normalize(e.TargetWord), normalizedQuery);
        if (rs < 0) return rt;
        if (rt < 0) return rs;
        return Math.Min(rs, rt);
    }

    private static int RankField(string field, string q)
    {
        if (field == q) return 0;                              // exakt
        if (field.StartsWith(q, StringComparison.Ordinal)) return 1;   // Präfix
        if (field.Contains(q, StringComparison.Ordinal)) return 2;     // enthält
        return -1;
    }

    private static string Normalize(string s)
    {
        var lower = s.Trim().ToLowerInvariant();
        var decomposed = lower.Normalize(NormalizationForm.FormD);
        return CombiningMarks.Replace(decomposed, "");
    }
}
