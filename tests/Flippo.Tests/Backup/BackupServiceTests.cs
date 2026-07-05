using Flippo.Core.Backup;
using Flippo.Core.Domain;
using Flippo.Data.Services;
using Flippo.Tests.Data;

namespace Flippo.Tests.Backup;

/// <summary>Import/Export über echte SQLite: Roundtrip, ID-Remapping, Skip, Safety-Export.</summary>
public class BackupServiceTests
{
    [Fact]
    public async Task ExportThenImport_RoundtripsData_AndRemapsSetIds()
    {
        using var db = new SqliteTestDatabase();
        var store = new VocabularyStore(db.Factory);
        var sessions = new SessionStore(db.Factory);
        var svc = new BackupService(db.Factory, TempDir());

        var setId = await store.AddSetAsync(new VocabularySet { Title = "S", SourceLanguage = "de", TargetLanguage = "es" });
        await store.AddEntryAsync(new VocabularyEntry
        {
            SetId = setId, SourceText = "Haus", TargetText = "casa",
            AcceptedAnswers = ["hogar"], Tags = ["a"], BoxLevel = 3, Difficulty = 260, LastIntervalDays = 7
        });
        await sessions.AddAsync(new SessionRecord { SetId = setId, SetName = "S", CorrectCount = 3, WrongCount = 1, StartedAt = 100, WrongEntryIds = "1,2", DurationMinutes = 2 });

        using var ms = new MemoryStream();
        await svc.ExportAsync(ms, new SrsSettings { Mode = SrsMode.FlashcardBox }, nowMs: 1000);

        ms.Position = 0;
        var parsed = await svc.ParseAsync(ms);
        var result = await svc.ImportAsync(parsed.Content, writeSafetyExport: false, nowMs: 2000);

        Assert.Equal(1, result.SetsImported);
        Assert.Equal(1, result.EntriesImported);
        Assert.Equal(0, result.EntriesSkipped);

        var set = Assert.Single(await store.GetSetsWithCountsAsync(nowMs: 9999));
        var entry = Assert.Single(await store.GetEntriesAsync(set.Id));
        Assert.Equal("casa", entry.TargetText);
        Assert.Equal(set.Id, entry.SetId);                 // auf neue Set-Id remappt
        Assert.Equal(new[] { "hogar" }, entry.AcceptedAnswers);
        Assert.Equal(7, entry.LastIntervalDays);

        var sess = await sessions.GetAllAsync();
        Assert.Equal("1,2", Assert.Single(sess).WrongEntryIds);   // NICHT remappt (Formatparität)
    }

    [Fact]
    public async Task Import_SkipsEntriesWithUnknownSet()
    {
        using var db = new SqliteTestDatabase();
        var svc = new BackupService(db.Factory, TempDir());
        var content = new BackupContent(
            Sets: [new VocabularySet { Id = 1, Title = "S" }],
            Entries:
            [
                new VocabularyEntry { Id = 1, SetId = 1, SourceText = "a", TargetText = "b" },   // ok
                new VocabularyEntry { Id = 2, SetId = 99, SourceText = "c", TargetText = "d" }   // unbekanntes Set → Skip
            ],
            Sessions: [],
            Settings: null);

        var result = await svc.ImportAsync(content, writeSafetyExport: false, nowMs: 0);

        Assert.Equal(1, result.EntriesImported);
        Assert.Equal(1, result.EntriesSkipped);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public async Task Import_WithSafetyExport_WritesPreImportFileOfOldData()
    {
        using var db = new SqliteTestDatabase();
        var store = new VocabularyStore(db.Factory);
        await store.AddSetAsync(new VocabularySet { Title = "AlterBestand" });

        var backupDir = TempDir();
        try
        {
            var svc = new BackupService(db.Factory, backupDir);
            var content = new BackupContent([new VocabularySet { Id = 1, Title = "Neu" }], [], [], null);

            await svc.ImportAsync(content, writeSafetyExport: true, nowMs: 1_700_000_000_000);

            var file = Assert.Single(Directory.GetFiles(backupDir, "pre-import-*.json"));
            Assert.Contains("AlterBestand", await File.ReadAllTextAsync(file));   // Safety-Export enthält ALTE Daten
        }
        finally
        {
            try { Directory.Delete(backupDir, recursive: true); } catch (IOException) { }
        }
    }

    private static string TempDir()
        => Path.Combine(Path.GetTempPath(), $"flippo-bktest-{Guid.NewGuid():N}");
}
