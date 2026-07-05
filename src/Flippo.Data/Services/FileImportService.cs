using Flippo.Core.Import;

namespace Flippo.Data.Services;

/// <summary>Ergebnis eines Datei-Imports: eingefügt, Duplikate (übersprungen), leere Zeilen, Gesamt.</summary>
public sealed record FileImportResult(int Imported, int Duplicates, int Skipped, int Total);

/// <summary>
/// Port von ImportVocabularyUseCase.importRows (data/importer). Mappt geparste Zeilen zu Karten,
/// filtert Duplikate (case-insensitive <c>sourceText|targetText</c> gegen die bestehenden Karten
/// des Ziel-Sets) und fügt den Rest ein. KEIN Free-Limit — Desktop ist kostenlos.
/// </summary>
public sealed class FileImportService
{
    private readonly VocabularyStore _store;

    public FileImportService(VocabularyStore store) => _store = store;

    public async Task<FileImportResult> ImportRowsAsync(
        IReadOnlyList<IReadOnlyList<string>> rows, long setId, ColumnMapping mapping, long nowMs,
        bool? treatFirstRowAsHeader = null, CancellationToken ct = default)
    {
        var (entries, skipped) = ImportEngine.MapToEntries(rows, setId, mapping, nowMs, treatFirstRowAsHeader);

        var existing = await _store.GetEntriesAsync(setId, ct);
        var existingKeys = existing
            .Select(e => DuplicateKey(e.SourceText, e.TargetText))
            .ToHashSet();

        var fresh = entries
            .Where(e => !existingKeys.Contains(DuplicateKey(e.SourceText, e.TargetText)))
            .ToList();
        int duplicates = entries.Count - fresh.Count;

        if (fresh.Count > 0)
            await _store.AddEntriesAsync(fresh, ct);

        return new FileImportResult(
            Imported: fresh.Count,
            Duplicates: duplicates,
            Skipped: skipped,
            Total: entries.Count + skipped);
    }

    private static string DuplicateKey(string source, string target) =>
        $"{source.ToLowerInvariant()}|{target.ToLowerInvariant()}";
}
