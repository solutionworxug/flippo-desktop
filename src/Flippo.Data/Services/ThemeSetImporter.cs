using Flippo.Core.Content;
using Flippo.Core.Domain;
using Flippo.Core.Import;

namespace Flippo.Data.Services;

/// <summary>Ergebnis eines Themenset-Imports.</summary>
public sealed record ThemeSetImportResult(long SetId, string Title, int EntryCount);

/// <summary>
/// Port von ThemeSetRepository.importAsSet: ein Themenset wird als normale Kartei mit Karten importiert.
/// Duplikat-Check über den Anzeigetitel; kein Update-/Merge-Pfad (wie Android). Kein Drip, kein Free-Limit
/// (Desktop ist kostenlos), keine Wörterbuch-Kopplung. Nutzt den vorhandenen P9-Mapper.
/// </summary>
public sealed class ThemeSetImporter
{
    private readonly IThemeSetSource _source;
    private readonly VocabularyStore _store;

    public ThemeSetImporter(IThemeSetSource source, VocabularyStore store)
    {
        _source = source;
        _store = store;
    }

    /// <summary>Verfügbare Themensets für eine Zielsprache ("Deutsch"/"Englisch").</summary>
    public async Task<IReadOnlyList<ThemeSetManifestEntry>> GetAvailableAsync(string targetLanguage)
    {
        var manifest = await _source.LoadManifestAsync();
        if (manifest is null) return [];
        return manifest.ThemeSets
            .Where(t => string.Equals(t.TargetLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Importiert ein Themenset unter <paramref name="displayTitle"/>. Rückgabe <c>null</c> = bereits
    /// importiert (Titel existiert) oder Datei nicht ladbar/leer.
    /// </summary>
    public async Task<ThemeSetImportResult?> ImportAsync(ThemeSetManifestEntry entry, string displayTitle, long nowMs)
    {
        var sets = await _store.GetSetsWithCountsAsync(nowMs);
        if (sets.Any(s => string.Equals(s.Title, displayTitle, StringComparison.OrdinalIgnoreCase)))
            return null;   // bereits importiert

        var file = await _source.LoadFileAsync(entry.Path);
        if (file is null || file.Entries.Count == 0) return null;

        long setId = await _store.AddSetAsync(new VocabularySet
        {
            Title = displayTitle,
            SourceLanguage = file.SourceLanguage,
            TargetLanguage = file.TargetLanguage,
            CreatedAt = nowMs,
            UpdatedAt = nowMs
        });

        // Zeilen [source, target, example, notes, tags, pos] durch den P9-Mapper
        // (" / "->acceptedAnswers, Tags ;/,). Keine Kopfzeile.
        var rows = file.Entries
            .Select(e => (IReadOnlyList<string>)new[] { e.Source, e.Target, e.Example, e.Notes, e.Tags, e.Pos })
            .ToList();
        var mapping = new ColumnMapping
        {
            SourceTextColumn = 0, TargetTextColumn = 1, ExampleSentenceColumn = 2,
            NotesColumn = 3, TagsColumn = 4, PartOfSpeechColumn = 5, SplitAlternatives = true
        };
        var (mapped, _) = ImportEngine.MapToEntries(rows, setId, mapping, nowMs, treatFirstRowAsHeader: false);
        await _store.AddEntriesAsync(mapped);

        return new ThemeSetImportResult(setId, displayTitle, mapped.Count);
    }
}
