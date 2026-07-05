using Flippo.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Flippo.Data.Services;

/// <summary>Fachlicher Store für Karteien und Karten (EF ist bereits Repo+UoW; keine zweite Abstraktion).</summary>
public sealed class VocabularyStore
{
    private readonly IDbContextFactory<FlippoDbContext> _factory;

    public VocabularyStore(IDbContextFactory<FlippoDbContext> factory) => _factory = factory;

    // ---- Sets ----

    /// <summary>Alle Sets mit aggregierten Zählern (gesamt/fällig/neu) in EINER Query (kein N+1).</summary>
    public async Task<IReadOnlyList<VocabularySet>> GetSetsWithCountsAsync(long nowMs, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.Sets.AsNoTracking()
            .Select(s => new
            {
                Set = s,
                Total = s.Entries.Count,
                Due = s.Entries.Count(e => !e.IsArchived && e.NextReviewAt <= nowMs),
                New = s.Entries.Count(e => !e.IsArchived && e.CorrectCount == 0 && e.WrongCount == 0)
            })
            .OrderBy(r => r.Set.Title)
            .ToListAsync(ct);

        return rows
            .Select(r => r.Set.ToDomain() with { TotalCards = r.Total, DueCards = r.Due, NewCards = r.New })
            .ToList();
    }

    public async Task<VocabularySet?> GetSetAsync(long id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Sets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return e?.ToDomain();
    }

    public async Task<long> AddSetAsync(VocabularySet set, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = set.ToEntity();
        e.Id = 0;
        db.Sets.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    public async Task UpdateSetAsync(VocabularySet set, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Sets.FirstOrDefaultAsync(x => x.Id == set.Id, ct)
            ?? throw new InvalidOperationException($"Set {set.Id} nicht gefunden");
        e.Title = set.Title;
        e.Description = set.Description;
        e.SourceLanguage = set.SourceLanguage;
        e.TargetLanguage = set.TargetLanguage;
        e.UpdatedAt = set.UpdatedAt;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteSetAsync(long id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Sets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return;
        db.Sets.Remove(e);   // Cascade löscht zugehörige Entries
        await db.SaveChangesAsync(ct);
    }

    // ---- Entries ----

    public async Task<IReadOnlyList<VocabularyEntry>> GetEntriesAsync(long setId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var list = await db.Entries.AsNoTracking()
            .Where(e => e.SetId == setId)
            .OrderBy(e => e.Id)
            .ToListAsync(ct);
        return list.Select(e => e.ToDomain()).ToList();
    }

    /// <summary>Alle Karten über Set-Grenzen — Einstieg "Alle lernen" und Fallback-Pool für MC-Distraktoren.</summary>
    public async Task<IReadOnlyList<VocabularyEntry>> GetAllEntriesAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var list = await db.Entries.AsNoTracking()
            .OrderBy(e => e.Id)
            .ToListAsync(ct);
        return list.Select(e => e.ToDomain()).ToList();
    }

    /// <summary>Karten zu einer ID-Menge — für "Falsche wiederholen" (SessionRecord.WrongEntryIds).</summary>
    public async Task<IReadOnlyList<VocabularyEntry>> GetEntriesByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];
        await using var db = await _factory.CreateDbContextAsync(ct);
        var list = await db.Entries.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(ct);
        return list.Select(e => e.ToDomain()).ToList();
    }

    public async Task<VocabularyEntry?> GetEntryAsync(long id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Entries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return e?.ToDomain();
    }

    public async Task<long> AddEntryAsync(VocabularyEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = entry.ToEntity();
        e.Id = 0;
        db.Entries.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    /// <summary>Fügt mehrere Karten in EINER Transaktion ein (Datei-Import, P9).</summary>
    public async Task AddEntriesAsync(IReadOnlyCollection<VocabularyEntry> entries, CancellationToken ct = default)
    {
        if (entries.Count == 0) return;
        await using var db = await _factory.CreateDbContextAsync(ct);
        foreach (var entry in entries)
        {
            var e = entry.ToEntity();
            e.Id = 0;
            db.Entries.Add(e);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateEntryAsync(VocabularyEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Entries.FirstOrDefaultAsync(x => x.Id == entry.Id, ct)
            ?? throw new InvalidOperationException($"Entry {entry.Id} nicht gefunden");

        var m = entry.ToEntity();
        e.SetId = m.SetId;
        e.SourceText = m.SourceText;
        e.TargetText = m.TargetText;
        e.AcceptedAnswers = m.AcceptedAnswers;
        e.ExampleSentence = m.ExampleSentence;
        e.Notes = m.Notes;
        e.PartOfSpeech = m.PartOfSpeech;
        e.Gender = m.Gender;
        e.PluralForm = m.PluralForm;
        e.VerbForms = m.VerbForms;
        e.Pronunciation = m.Pronunciation;
        e.Tags = m.Tags;
        e.Mnemonic = m.Mnemonic;
        e.ImagePath = m.ImagePath;
        e.AudioPath = m.AudioPath;
        e.Difficulty = m.Difficulty;
        e.BoxLevel = m.BoxLevel;
        e.NextReviewAt = m.NextReviewAt;
        e.CorrectCount = m.CorrectCount;
        e.WrongCount = m.WrongCount;
        e.LastReviewedAt = m.LastReviewedAt;
        e.CreatedAt = m.CreatedAt;
        e.UpdatedAt = m.UpdatedAt;
        e.IsArchived = m.IsArchived;
        e.IsLeech = m.IsLeech;
        e.LastIntervalDays = m.LastIntervalDays;

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteEntryAsync(long id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Entries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return;
        db.Entries.Remove(e);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Wendet ein SrsEngine-Ergebnis auf die Karte an.</summary>
    public async Task ApplyReviewAsync(VocabularyEntryUpdate update, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Entries.FirstOrDefaultAsync(x => x.Id == update.Id, ct)
            ?? throw new InvalidOperationException($"Entry {update.Id} nicht gefunden");
        e.BoxLevel = update.BoxLevel;
        e.NextReviewAt = update.NextReviewAt;
        e.CorrectCount = update.CorrectCount;
        e.WrongCount = update.WrongCount;
        e.LastReviewedAt = update.LastReviewedAt;
        e.IsLeech = update.IsLeech;
        e.Difficulty = update.Difficulty;
        e.LastIntervalDays = update.LastIntervalDays;
        e.UpdatedAt = update.UpdatedAt;
        await db.SaveChangesAsync(ct);
    }
}
