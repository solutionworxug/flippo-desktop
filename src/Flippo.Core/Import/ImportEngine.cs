using System.Text;
using Flippo.Core.Domain;

namespace Flippo.Core.Import;

/// <summary>
/// Port von ImportEngine.kt (data/importer) — CSV/TSV-Parsing + Zeilen→<see cref="VocabularyEntry"/>-Mapping.
/// Reine BCL-Logik. XLSX gehört bewusst NICHT hierher: der ClosedXML-basierte Reader liegt in der
/// Data-/App-Schicht (Flippo.Core hat keine NuGet-Deps) und reicht seine Zeilen an <see cref="MapToEntries"/>.
/// </summary>
public static class ImportEngine
{
    /// <summary>
    /// Parst CSV- oder TSV-Inhalt zu Zeilen. Behandelt gequotete Felder mit enthaltenem Delimiter
    /// und verdoppelte Quotes (<c>""</c>) als Escape. Leerzeilen werden verworfen, Felder getrimmt.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> ParseDelimited(string content, char delimiter = ',')
    {
        var rows = new List<IReadOnlyList<string>>();
        // Kotlin content.lines() splittet an \n / \r\n / \r → hier normalisieren, dann an \n splitten.
        var lines = content.ReplaceLineEndings("\n").Split('\n');

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;   // Kotlin: filter { it.isNotBlank() }

            var fields = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            int i = 0;

            while (i < line.Length)
            {
                char ch = line[i];
                if (ch == '"' && inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote: ein " übernehmen, das zweite überspringen
                    current.Append('"');
                    i += 2;
                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (ch == delimiter && !inQuotes)
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
                i++;
            }
            fields.Add(current.ToString().Trim());
            if (fields.Any(f => !string.IsNullOrWhiteSpace(f)))
                rows.Add(fields);
        }

        return rows;
    }

    /// <summary>
    /// Mappt geparste Zeilen auf <see cref="VocabularyEntry"/> nach <see cref="ColumnMapping"/>.
    /// Zeitstempel per injiziertem <paramref name="nowMs"/> (pure Core: kein <c>System.currentTimeMillis()</c>).
    /// <paramref name="treatFirstRowAsHeader"/>: <c>null</c> = Auto-Erkennung via typische Header-Namen
    /// (Android-Verhalten), <c>true</c>/<c>false</c> = expliziter UI-Toggle. Rückgabe = (Einträge, übersprungen).
    /// </summary>
    public static (IReadOnlyList<VocabularyEntry> Entries, int Skipped) MapToEntries(
        IReadOnlyList<IReadOnlyList<string>> rows, long setId, ColumnMapping mapping, long nowMs,
        bool? treatFirstRowAsHeader = null)
    {
        if (rows.Count == 0) return ([], 0);

        bool skipHeader = treatFirstRowAsHeader ?? IsHeaderRow(rows[0]);
        int startIndex = skipHeader ? 1 : 0;
        int skipped = 0;
        var result = new List<VocabularyEntry>();

        for (int r = startIndex; r < rows.Count; r++)
        {
            var row = rows[r];
            string? source = GetOrNull(row, mapping.SourceTextColumn)?.Trim();
            string? rawTarget = GetOrNull(row, mapping.TargetTextColumn)?.Trim();
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(rawTarget))
            {
                skipped++;
                continue;
            }

            string targetText;
            IReadOnlyList<string> acceptedAnswers;
            if (mapping.SplitAlternatives && rawTarget.Contains(" / "))
            {
                var parts = rawTarget.Split(" / ")
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();
                targetText = parts[0];
                acceptedAnswers = parts.Skip(1).ToList();
            }
            else
            {
                targetText = rawTarget;
                acceptedAnswers = [];
            }

            result.Add(new VocabularyEntry
            {
                SetId = setId,
                SourceText = source,
                TargetText = targetText,
                AcceptedAnswers = acceptedAnswers,
                ExampleSentence = mapping.ExampleSentenceColumn >= 0
                    ? GetOrNull(row, mapping.ExampleSentenceColumn)?.Trim() ?? "" : "",
                Notes = mapping.NotesColumn >= 0
                    ? GetOrNull(row, mapping.NotesColumn)?.Trim() ?? "" : "",
                Tags = mapping.TagsColumn >= 0
                    ? SplitTags(GetOrNull(row, mapping.TagsColumn)) : [],
                PartOfSpeech = mapping.PartOfSpeechColumn >= 0
                    ? GetOrNull(row, mapping.PartOfSpeechColumn)?.Trim() ?? "" : "",
                CreatedAt = nowMs,
                UpdatedAt = nowMs
            });
        }

        return (result, skipped);
    }

    private static readonly string[] HeaderKeywords =
    {
        "source", "target", "word", "translation", "vokabel",
        "übersetzung", "deutsch", "englisch", "spanisch", "french"
    };

    /// <summary>Heuristik: enthält die Zeile typische Kopfzeilen-Begriffe? (Vorbelegung des UI-Toggles.)</summary>
    public static bool IsHeaderRow(IReadOnlyList<string> row) =>
        row.Any(field => HeaderKeywords.Any(keyword => field.ToLowerInvariant().Contains(keyword)));

    private static IReadOnlyList<string> SplitTags(string? raw)
    {
        if (raw is null) return [];
        return raw.Split(';', ',')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
    }

    private static string? GetOrNull(IReadOnlyList<string> row, int index) =>
        index >= 0 && index < row.Count ? row[index] : null;
}
