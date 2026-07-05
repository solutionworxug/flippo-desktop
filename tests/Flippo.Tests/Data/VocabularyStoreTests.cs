using Flippo.Core.Domain;
using Flippo.Data.Services;

namespace Flippo.Tests.Data;

/// <summary>Store-Tests: CRUD, Cascade, List-Roundtrip gegen echte SQLite (P2-Verify).</summary>
public class VocabularyStoreTests
{
    private static VocabularyStore StoreFor(SqliteTestDatabase db) => new(db.Factory);
    private static SessionStore SessionStoreFor(SqliteTestDatabase db) => new(db.Factory);

    [Fact]
    public async Task AddSet_AssignsIdAndRoundtrips()
    {
        using var db = new SqliteTestDatabase();
        var store = StoreFor(db);

        var id = await store.AddSetAsync(new VocabularySet { Title = "Spanisch", SourceLanguage = "de", TargetLanguage = "es" });
        Assert.True(id > 0);

        var loaded = await store.GetSetAsync(id);
        Assert.NotNull(loaded);
        Assert.Equal("Spanisch", loaded!.Title);
        Assert.Equal("es", loaded.TargetLanguage);
    }

    [Fact]
    public async Task AddEntry_ListRoundtrip_AcceptedAnswersAndTags()
    {
        using var db = new SqliteTestDatabase();
        var store = StoreFor(db);
        var setId = await store.AddSetAsync(new VocabularySet { Title = "Set" });

        var entryId = await store.AddEntryAsync(new VocabularyEntry
        {
            SetId = setId,
            SourceText = "Haus",
            TargetText = "casa",
            AcceptedAnswers = ["la casa", "el hogar"],
            Tags = ["nomen", "wohnen"]
        });

        var loaded = await store.GetEntryAsync(entryId);
        Assert.NotNull(loaded);
        Assert.Equal(new[] { "la casa", "el hogar" }, loaded!.AcceptedAnswers);
        Assert.Equal(new[] { "nomen", "wohnen" }, loaded.Tags);
    }

    [Fact]
    public async Task AddEntry_EmptyLists_RoundtripAsEmpty()
    {
        using var db = new SqliteTestDatabase();
        var store = StoreFor(db);
        var setId = await store.AddSetAsync(new VocabularySet { Title = "Set" });

        var entryId = await store.AddEntryAsync(new VocabularyEntry { SetId = setId, SourceText = "a", TargetText = "b" });

        var loaded = await store.GetEntryAsync(entryId);
        Assert.NotNull(loaded);
        Assert.Empty(loaded!.AcceptedAnswers);
        Assert.Empty(loaded.Tags);
    }

    [Fact]
    public async Task DeleteSet_CascadeRemovesEntries()
    {
        using var db = new SqliteTestDatabase();
        var store = StoreFor(db);
        var setId = await store.AddSetAsync(new VocabularySet { Title = "Set" });
        await store.AddEntryAsync(new VocabularyEntry { SetId = setId, SourceText = "a", TargetText = "b" });
        await store.AddEntryAsync(new VocabularyEntry { SetId = setId, SourceText = "c", TargetText = "d" });

        await store.DeleteSetAsync(setId);

        Assert.Null(await store.GetSetAsync(setId));
        Assert.Empty(await store.GetEntriesAsync(setId));
    }

    [Fact]
    public async Task UpdateEntry_PersistsChanges()
    {
        using var db = new SqliteTestDatabase();
        var store = StoreFor(db);
        var setId = await store.AddSetAsync(new VocabularySet { Title = "Set" });
        var entryId = await store.AddEntryAsync(new VocabularyEntry { SetId = setId, SourceText = "a", TargetText = "b" });

        var entry = await store.GetEntryAsync(entryId);
        await store.UpdateEntryAsync(entry! with { TargetText = "geändert", AcceptedAnswers = ["alt"], Notes = "hinweis" });

        var reloaded = await store.GetEntryAsync(entryId);
        Assert.Equal("geändert", reloaded!.TargetText);
        Assert.Equal(new[] { "alt" }, reloaded.AcceptedAnswers);
        Assert.Equal("hinweis", reloaded.Notes);
    }

    [Fact]
    public async Task ApplyReview_UpdatesSrsFields()
    {
        using var db = new SqliteTestDatabase();
        var store = StoreFor(db);
        var setId = await store.AddSetAsync(new VocabularySet { Title = "Set" });
        var entryId = await store.AddEntryAsync(new VocabularyEntry { SetId = setId, SourceText = "a", TargetText = "b", BoxLevel = 1 });

        await store.ApplyReviewAsync(new VocabularyEntryUpdate
        {
            Id = entryId,
            BoxLevel = 3,
            NextReviewAt = 999_000,
            CorrectCount = 2,
            WrongCount = 1,
            LastReviewedAt = 500,
            IsLeech = true,
            Difficulty = 260,
            LastIntervalDays = 7,
            UpdatedAt = 500
        });

        var e = await store.GetEntryAsync(entryId);
        Assert.Equal(3, e!.BoxLevel);
        Assert.Equal(999_000, e.NextReviewAt);
        Assert.Equal(2, e.CorrectCount);
        Assert.True(e.IsLeech);
        Assert.Equal(7, e.LastIntervalDays);
    }

    [Fact]
    public async Task GetSetsWithCounts_AggregatesTotalDueNew()
    {
        using var db = new SqliteTestDatabase();
        var store = StoreFor(db);
        const long now = 1000;
        var setId = await store.AddSetAsync(new VocabularySet { Title = "Zähl-Set" });

        // e1: neu (0/0) + fällig (nextReview 0)                → Total, Due, New
        await store.AddEntryAsync(new VocabularyEntry { SetId = setId, SourceText = "1", TargetText = "1", CorrectCount = 0, WrongCount = 0, NextReviewAt = 0 });
        // e2: gelernt, fällig (nextReview 500)                 → Total, Due
        await store.AddEntryAsync(new VocabularyEntry { SetId = setId, SourceText = "2", TargetText = "2", CorrectCount = 1, WrongCount = 0, NextReviewAt = 500 });
        // e3: gelernt, nicht fällig (nextReview 5000)          → Total
        await store.AddEntryAsync(new VocabularyEntry { SetId = setId, SourceText = "3", TargetText = "3", CorrectCount = 1, WrongCount = 0, NextReviewAt = 5000 });
        // e4: archiviert (aus Due/New ausgeschlossen)          → nur Total
        await store.AddEntryAsync(new VocabularyEntry { SetId = setId, SourceText = "4", TargetText = "4", CorrectCount = 0, WrongCount = 0, NextReviewAt = 0, IsArchived = true });

        var sets = await store.GetSetsWithCountsAsync(now);
        var set = Assert.Single(sets);
        Assert.Equal(4, set.TotalCards);
        Assert.Equal(2, set.DueCards);
        Assert.Equal(1, set.NewCards);
    }

    [Fact]
    public async Task SessionStore_AddAndGetAll()
    {
        using var db = new SqliteTestDatabase();
        var sessions = SessionStoreFor(db);

        await sessions.AddAsync(new SessionRecord { SetId = null, SetName = "Alle", CorrectCount = 5, WrongCount = 2, StartedAt = 100, DurationMinutes = 3, LearningMode = "FLASHCARD" });
        await sessions.AddAsync(new SessionRecord { SetId = 1, SetName = "Set A", CorrectCount = 1, WrongCount = 0, StartedAt = 200, LearningMode = "FREE_TEXT" });

        var all = await sessions.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(200, all[0].StartedAt);   // neueste zuerst
        Assert.Equal("FLASHCARD", all[1].LearningMode);
    }
}
