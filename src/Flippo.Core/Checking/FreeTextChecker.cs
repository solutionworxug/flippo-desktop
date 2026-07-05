using System.Text;
using Flippo.Core.Domain;
using Flippo.Core.Util;

namespace Flippo.Core.Checking;

/// <summary>1:1-Port von FreeTextChecker.kt. Prüft Freitext-Antworten mit konfigurierbarer Toleranz.</summary>
public static class FreeTextChecker
{
    public enum CheckResult
    {
        Correct,
        AlmostCorrect,   // nur Akzent-Abweichung
        Typo,            // nah dran, nicht exakt
        Wrong
    }

    public sealed record CheckOutcome(CheckResult Result, string CorrectAnswer);

    public static CheckOutcome Check(
        string userInput,
        VocabularyEntry entry,
        bool strictAccents,
        bool typoToleranceEnabled,
        IReadOnlyList<string>? siblingAnswers = null)
    {
        siblingAnswers ??= [];
        string input = Collapse(userInput);
        var candidates = BuildCandidates(entry);

        // 1. Exakt (case-insensitive)
        foreach (var candidate in candidates)
        {
            if (string.Equals(input, candidate, StringComparison.OrdinalIgnoreCase))
                return new CheckOutcome(CheckResult.Correct, candidate);
        }

        // 2. Akzent-normalisiert
        if (!strictAccents)
        {
            string normalizedInput = Normalize(input);
            foreach (var candidate in candidates)
            {
                if (string.Equals(normalizedInput, Normalize(candidate), StringComparison.OrdinalIgnoreCase))
                    return new CheckOutcome(CheckResult.AlmostCorrect, candidate);
            }
        }

        // 3. Tippfehler-Toleranz (Levenshtein), Schwelle nach KANDIDATEN-Länge: ≤8→0, ≤12→1, sonst 2.
        if (typoToleranceEnabled)
        {
            foreach (var candidate in candidates)
            {
                int maxDistance = candidate.Length <= 8 ? 0 : candidate.Length <= 12 ? 1 : 2;
                if (maxDistance == 0) continue;  // 5–8 Zeichen: keine Toleranz mehr

                int dist = Levenshtein(input.ToLowerInvariant(), candidate.ToLowerInvariant());
                if (dist >= 1 && dist <= maxDistance &&
                    !CollidesWithSibling(input, candidate, siblingAnswers, strictAccents))
                {
                    return new CheckOutcome(CheckResult.Typo, candidate);
                }

                // Auch mit normalisierten Versionen prüfen
                if (!strictAccents)
                {
                    int distNorm = Levenshtein(
                        Normalize(input).ToLowerInvariant(),
                        Normalize(candidate).ToLowerInvariant());
                    if (distNorm >= 1 && distNorm <= maxDistance &&
                        !CollidesWithSibling(input, candidate, siblingAnswers, strictAccents))
                    {
                        return new CheckOutcome(CheckResult.Typo, candidate);
                    }
                }
            }
        }

        string bestCandidate = candidates.Count > 0 ? candidates[0] : entry.TargetText;
        return new CheckOutcome(CheckResult.Wrong, bestCandidate);
    }

    /// <summary>
    /// Geschwister-Kollision: Eingabe = exakte (oder akzent-normalisierte) Antwort einer ANDEREN
    /// Karte → Verwechselung, kein Tippfehler. Verhindert pädagogisch falsche "fast richtig"-Wertung.
    /// </summary>
    private static bool CollidesWithSibling(
        string input,
        string currentCandidate,
        IReadOnlyList<string> siblings,
        bool strictAccents)
    {
        if (siblings.Count == 0) return false;
        string normInput = strictAccents ? input : Normalize(input);
        string normCurrent = strictAccents ? currentCandidate : Normalize(currentCandidate);
        return siblings.Any(sibling =>
        {
            string normSibling = strictAccents ? sibling : Normalize(sibling);
            return string.Equals(normSibling, normInput, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normSibling, normCurrent, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static List<string> BuildCandidates(VocabularyEntry entry)
    {
        var all = new List<string> { entry.TargetText };
        all.AddRange(entry.AcceptedAnswers);
        return all.Where(s => !string.IsNullOrWhiteSpace(s))
                  .Select(Collapse)
                  .Distinct()
                  .ToList();
    }

    /// <summary>trim + Whitespace-Kollaps (nur ASCII-Whitespace, Java <c>\s+</c>).</summary>
    private static string Collapse(string text)
        => JavaCompat.AsciiWhitespace().Replace(text.Trim(), " ");

    /// <summary>Entfernt Diakritika via NFD + Combining-Diacritical-Marks-Block. sí → si, über → uber.</summary>
    public static string Normalize(string text)
    {
        string decomposed = text.Normalize(NormalizationForm.FormD);
        return JavaCompat.CombiningDiacriticalMarks().Replace(decomposed, "");
    }

    /// <summary>Levenshtein-Editierdistanz (Standard-DP, identisch zum Kotlin-Original).</summary>
    public static int Levenshtein(string a, string b)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }
        return dp[a.Length, b.Length];
    }
}
