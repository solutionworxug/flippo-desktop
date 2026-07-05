using Flippo.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Flippo.Data.Services;

/// <summary>Fachlicher Store für Nachschlagewerke und deren Einträge (P13). Nicht im Backup (Android-Parität).</summary>
public sealed class UserDictionaryStore
{
    private readonly IDbContextFactory<FlippoDbContext> _factory;

    public UserDictionaryStore(IDbContextFactory<FlippoDbContext> factory) => _factory = factory;

    // ---- Wörterbücher ----

    public async Task<IReadOnlyList<UserDictionary>> GetDictionariesAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.UserDictionaries.AsNoTracking()
            .Select(d => new { Dict = d, Count = d.Entries.Count })
            .OrderBy(x => x.Dict.Name)
            .ToListAsync(ct);
        return rows.Select(x => x.Dict.ToDomain(x.Count)).ToList();
    }

    public async Task<UserDictionary?> GetDictionaryAsync(long id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var d = await db.UserDictionaries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return null;
        int count = await db.UserDictionaryEntries.CountAsync(e => e.DictionaryId == id, ct);
        return d.ToDomain(count);
    }

    public async Task<long> AddDictionaryAsync(UserDictionary dict, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = dict.ToEntity();
        e.Id = 0;
        db.UserDictionaries.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    public async Task DeleteDictionaryAsync(long id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.UserDictionaries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return;
        db.UserDictionaries.Remove(e);   // Cascade löscht Einträge
        await db.SaveChangesAsync(ct);
    }

    // ---- Einträge ----

    public async Task<IReadOnlyList<UserDictionaryEntry>> GetEntriesAsync(long dictId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var list = await db.UserDictionaryEntries.AsNoTracking()
            .Where(e => e.DictionaryId == dictId)
            .OrderBy(e => e.SourceWord)
            .ToListAsync(ct);
        return list.Select(e => e.ToDomain()).ToList();
    }

    public async Task<long> AddEntryAsync(UserDictionaryEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = entry.ToEntity();
        e.Id = 0;
        db.UserDictionaryEntries.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    public async Task UpdateEntryAsync(UserDictionaryEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.UserDictionaryEntries.FirstOrDefaultAsync(x => x.Id == entry.Id, ct)
            ?? throw new InvalidOperationException($"UserDictionaryEntry {entry.Id} nicht gefunden");

        var m = entry.ToEntity();
        e.SourceWord = m.SourceWord;
        e.TargetWord = m.TargetWord;
        e.PartOfSpeech = m.PartOfSpeech;
        e.Gender = m.Gender;
        e.ExampleSentence = m.ExampleSentence;
        e.ExampleTranslation = m.ExampleTranslation;
        e.Level = m.Level;
        e.AcceptedAnswers = m.AcceptedAnswers;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteEntryAsync(long id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.UserDictionaryEntries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return;
        db.UserDictionaryEntries.Remove(e);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Batch-Einfügen unter eine Wörterbuch-ID (Bundled-Install).</summary>
    public async Task AddEntriesAsync(long dictId, IReadOnlyCollection<UserDictionaryEntry> entries, CancellationToken ct = default)
    {
        if (entries.Count == 0) return;
        await using var db = await _factory.CreateDbContextAsync(ct);
        foreach (var entry in entries)
        {
            var e = entry.ToEntity();
            e.Id = 0;
            e.DictionaryId = dictId;
            db.UserDictionaryEntries.Add(e);
        }
        await db.SaveChangesAsync(ct);
    }
}
