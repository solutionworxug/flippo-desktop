using Flippo.Core.Content;
using Flippo.Core.Domain;

namespace Flippo.Data.Services;

/// <summary>
/// Installiert ein gebündeltes Wörterbuch als Nutzer-Wörterbuch (Port von importBundledLanguage).
/// Duplikat-Check über Name + Zielsprache; null = bereits installiert oder Datei nicht ladbar.
/// </summary>
public sealed class DictionaryInstaller
{
    private readonly IBundledDictionarySource _source;
    private readonly UserDictionaryStore _store;

    public DictionaryInstaller(IBundledDictionarySource source, UserDictionaryStore store)
    {
        _source = source;
        _store = store;
    }

    public async Task<long?> InstallAsync(BundledDictionaryInfo info, long nowMs)
    {
        var existing = await _store.GetDictionariesAsync();
        if (existing.Any(d => d.Name == info.SourceLanguage && d.TargetLanguage == info.TargetLanguage))
            return null;   // bereits installiert

        var file = await _source.LoadAsync(info.AssetPath);
        if (file is null || file.Entries.Count == 0) return null;

        long dictId = await _store.AddDictionaryAsync(new UserDictionary
        {
            Name = info.SourceLanguage,
            SourceLanguage = info.SourceLanguage,
            TargetLanguage = info.TargetLanguage,
            CreatedAt = nowMs
        });

        var entries = file.Entries.Select(e => new UserDictionaryEntry
        {
            DictionaryId = dictId,
            SourceWord = e.Source,
            TargetWord = e.Target,
            PartOfSpeech = e.Pos,
            Gender = e.Gender,
            ExampleSentence = e.Example
        }).ToList();
        await _store.AddEntriesAsync(dictId, entries);

        return dictId;
    }
}
