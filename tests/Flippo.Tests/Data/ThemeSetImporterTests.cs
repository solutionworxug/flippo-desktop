using Flippo.Core.Content;
using Flippo.Data.Services;

namespace Flippo.Tests.Data;

/// <summary>
/// ThemeSetImporter (Port von ThemeSetRepository.importAsSet) gegen echte SQLite + Fake-Quelle:
/// Sprachfilter, Mapping (Slash-Alternativen/Tags/PoS über den P9-Pfad), Duplikat-Check.
/// </summary>
public class ThemeSetImporterTests
{
    private const long Now = 1_700_000_000_000L;

    private sealed class FakeSource : IThemeSetSource
    {
        private readonly ThemeSetManifest _manifest;
        private readonly Dictionary<string, ThemeSetFile> _files;
        public FakeSource(ThemeSetManifest manifest, Dictionary<string, ThemeSetFile> files)
        { _manifest = manifest; _files = files; }
        public Task<ThemeSetManifest?> LoadManifestAsync() => Task.FromResult<ThemeSetManifest?>(_manifest);
        public Task<ThemeSetFile?> LoadFileAsync(string path) => Task.FromResult(_files.GetValueOrDefault(path));
    }

    private static (ThemeSetImporter importer, VocabularyStore store) Make(
        SqliteTestDatabase db, ThemeSetManifest manifest, Dictionary<string, ThemeSetFile> files)
    {
        var store = new VocabularyStore(db.Factory);
        return (new ThemeSetImporter(new FakeSource(manifest, files), store), store);
    }

    [Fact]
    public async Task GetAvailable_FiltersByTargetLanguage()
    {
        using var db = new SqliteTestDatabase();
        var manifest = new ThemeSetManifest
        {
            ThemeSets =
            {
                new() { Id = "en-farben", Language = "EN", TargetLanguage = "Deutsch", Title = "Farben", Path = "themesets/en/farben.json" },
                new() { Id = "de-colors", Language = "DE", TargetLanguage = "Englisch", Title = "Colors", Path = "themesets/de/colors.json" },
            }
        };
        var (imp, _) = Make(db, manifest, new());

        var de = await imp.GetAvailableAsync("Deutsch");

        Assert.Single(de);
        Assert.Equal("en-farben", de[0].Id);
    }

    [Fact]
    public async Task Import_CreatesSet_WithSlashTagsAndPos()
    {
        using var db = new SqliteTestDatabase();
        var entry = new ThemeSetManifestEntry { Id = "en-farben", Language = "EN", TargetLanguage = "Deutsch", Title = "Farben", Path = "themesets/en/farben.json" };
        var file = new ThemeSetFile
        {
            SourceLanguage = "Englisch", TargetLanguage = "Deutsch", Title = "Farben",
            Entries =
            {
                new() { Source = "red", Target = "rot", Example = "A red car.", Pos = "Adjektiv", Tags = "farbe,grund" },
                new() { Source = "big", Target = "groß / riesig", Example = "", Pos = "Adjektiv", Tags = "" },
            }
        };
        var (imp, store) = Make(db, new ThemeSetManifest { ThemeSets = { entry } },
            new() { ["themesets/en/farben.json"] = file });

        var result = await imp.ImportAsync(entry, "Farben (EN)", Now);

        Assert.NotNull(result);
        Assert.Equal(2, result!.EntryCount);
        var entries = await store.GetEntriesAsync(result.SetId);
        var red = entries.First(e => e.SourceText == "red");
        Assert.Equal("rot", red.TargetText);
        Assert.Equal("A red car.", red.ExampleSentence);
        Assert.Equal("Adjektiv", red.PartOfSpeech);
        Assert.Equal(new[] { "farbe", "grund" }, red.Tags);
        var big = entries.First(e => e.SourceText == "big");
        Assert.Equal("groß", big.TargetText);
        Assert.Equal(new[] { "riesig" }, big.AcceptedAnswers);
    }

    [Fact]
    public async Task Import_Duplicate_ReturnsNull()
    {
        using var db = new SqliteTestDatabase();
        var entry = new ThemeSetManifestEntry { Id = "en-farben", Language = "EN", TargetLanguage = "Deutsch", Title = "Farben", Path = "p.json" };
        var file = new ThemeSetFile { SourceLanguage = "Englisch", TargetLanguage = "Deutsch", Entries = { new() { Source = "red", Target = "rot" } } };
        var (imp, _) = Make(db, new ThemeSetManifest { ThemeSets = { entry } }, new() { ["p.json"] = file });

        Assert.NotNull(await imp.ImportAsync(entry, "Farben (EN)", Now));
        Assert.Null(await imp.ImportAsync(entry, "Farben (EN)", Now));   // gleicher Titel → bereits importiert
    }
}
