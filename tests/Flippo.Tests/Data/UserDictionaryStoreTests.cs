using Flippo.Core.Content;
using Flippo.Core.Domain;
using Flippo.Data.Services;

namespace Flippo.Tests.Data;

/// <summary>UserDictionaryStore + DictionaryInstaller gegen echte SQLite (inkl. neuer Migration).</summary>
public class UserDictionaryStoreTests
{
    [Fact]
    public async Task AddDictionary_AndEntries_RoundtripWithCount()
    {
        using var db = new SqliteTestDatabase();
        var store = new UserDictionaryStore(db.Factory);
        var dictId = await store.AddDictionaryAsync(new UserDictionary
        { Name = "Englisch", SourceLanguage = "Englisch", TargetLanguage = "Deutsch", CreatedAt = 1 });

        await store.AddEntriesAsync(dictId, new[]
        {
            new UserDictionaryEntry { SourceWord = "hello", TargetWord = "hallo", AcceptedAnswers = ["hi"] },
            new UserDictionaryEntry { SourceWord = "house", TargetWord = "Haus" },
        });

        var dicts = await store.GetDictionariesAsync();
        Assert.Single(dicts);
        Assert.Equal(2, dicts[0].EntryCount);

        var entries = await store.GetEntriesAsync(dictId);
        Assert.Equal(2, entries.Count);
        Assert.Equal(new[] { "hi" }, entries.First(e => e.SourceWord == "hello").AcceptedAnswers);
    }

    [Fact]
    public async Task DeleteDictionary_CascadesEntries()
    {
        using var db = new SqliteTestDatabase();
        var store = new UserDictionaryStore(db.Factory);
        var dictId = await store.AddDictionaryAsync(new UserDictionary { Name = "X" });
        await store.AddEntryAsync(new UserDictionaryEntry { DictionaryId = dictId, SourceWord = "a", TargetWord = "b" });

        await store.DeleteDictionaryAsync(dictId);

        Assert.Empty(await store.GetEntriesAsync(dictId));
        Assert.Empty(await store.GetDictionariesAsync());
    }

    [Fact]
    public async Task UpdateEntry_Persists()
    {
        using var db = new SqliteTestDatabase();
        var store = new UserDictionaryStore(db.Factory);
        var dictId = await store.AddDictionaryAsync(new UserDictionary { Name = "X" });
        var id = await store.AddEntryAsync(new UserDictionaryEntry { DictionaryId = dictId, SourceWord = "a", TargetWord = "b" });

        await store.UpdateEntryAsync(new UserDictionaryEntry { Id = id, DictionaryId = dictId, SourceWord = "a", TargetWord = "bb", PartOfSpeech = "Noun" });

        var e = (await store.GetEntriesAsync(dictId)).Single();
        Assert.Equal("bb", e.TargetWord);
        Assert.Equal("Noun", e.PartOfSpeech);
    }

    [Fact]
    public async Task Installer_CreatesDict_ThenDedupes()
    {
        using var db = new SqliteTestDatabase();
        var store = new UserDictionaryStore(db.Factory);
        var info = new BundledDictionaryInfo("Englisch", "Deutsch", "dicts/englisch.json");
        var file = new BundledDictionaryFile
        {
            Name = "Englisch", SourceLanguage = "Englisch", TargetLanguage = "Deutsch",
            Entries = { new() { Source = "hello", Target = "hallo", Pos = "Interjektion", Example = "Hello!" } }
        };
        var installer = new DictionaryInstaller(new FakeDictSource(new() { ["dicts/englisch.json"] = file }), store);

        var id = await installer.InstallAsync(info, 1);
        Assert.NotNull(id);
        var entries = await store.GetEntriesAsync(id!.Value);
        Assert.Single(entries);
        Assert.Equal("hallo", entries[0].TargetWord);
        Assert.Equal("Interjektion", entries[0].PartOfSpeech);

        Assert.Null(await installer.InstallAsync(info, 2));   // gleicher Name+Zielsprache → bereits installiert
    }

    private sealed class FakeDictSource : IBundledDictionarySource
    {
        private readonly Dictionary<string, BundledDictionaryFile> _files;
        public FakeDictSource(Dictionary<string, BundledDictionaryFile> files) => _files = files;
        public Task<BundledDictionaryFile?> LoadAsync(string path) => Task.FromResult(_files.GetValueOrDefault(path));
    }
}
