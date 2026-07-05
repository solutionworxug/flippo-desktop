using Flippo.Core.Domain;
using Flippo.Core.Import;
using Flippo.Data.Services;

namespace Flippo.Tests.Data;

/// <summary>
/// FileImportService (Port von ImportVocabularyUseCase.importRows) gegen echte SQLite:
/// Mapping + Duplikat-Filter (case-insensitive) + Insert + Zählungen.
/// </summary>
public class FileImportServiceTests
{
    private const long Now = 1_700_000_000_000L;

    private static List<IReadOnlyList<string>> Rows(params string[][] rows) =>
        rows.Cast<IReadOnlyList<string>>().ToList();

    [Fact]
    public async Task ImportRows_InsertsMappedEntries()
    {
        using var db = new SqliteTestDatabase();
        var store = new VocabularyStore(db.Factory);
        var import = new FileImportService(store);
        var setId = await store.AddSetAsync(new VocabularySet { Title = "Set" });

        var result = await import.ImportRowsAsync(
            Rows(["cat", "Katze"], ["dog", "Hund"]), setId, new ColumnMapping(), Now);

        Assert.Equal(2, result.Imported);
        Assert.Equal(0, result.Duplicates);
        Assert.Equal(2, (await store.GetEntriesAsync(setId)).Count);
    }

    [Fact]
    public async Task ImportRows_SkipsDuplicatesAgainstExistingCaseInsensitive()
    {
        using var db = new SqliteTestDatabase();
        var store = new VocabularyStore(db.Factory);
        var import = new FileImportService(store);
        var setId = await store.AddSetAsync(new VocabularySet { Title = "Set" });
        await store.AddEntryAsync(new VocabularyEntry { SetId = setId, SourceText = "cat", TargetText = "Katze" });

        // "CAT"/"katze" = Duplikat (case-insensitive), "dog"/"Hund" = neu
        var result = await import.ImportRowsAsync(
            Rows(["CAT", "katze"], ["dog", "Hund"]), setId, new ColumnMapping(), Now);

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Duplicates);
        Assert.Equal(2, (await store.GetEntriesAsync(setId)).Count);
    }

    [Fact]
    public async Task ImportRows_CountsSkippedBlankRows()
    {
        using var db = new SqliteTestDatabase();
        var store = new VocabularyStore(db.Factory);
        var import = new FileImportService(store);
        var setId = await store.AddSetAsync(new VocabularySet { Title = "Set" });

        var result = await import.ImportRowsAsync(
            Rows(["cat", "Katze"], ["dog", ""]), setId, new ColumnMapping(), Now);

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(2, result.Total);
    }
}
