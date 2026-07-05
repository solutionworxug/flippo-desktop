using Flippo.Core.Backup;
using Flippo.Core.Domain;

namespace Flippo.Tests.Backup;

public class BackupRoundtripTests
{
    [Fact]
    public void FullRoundtrip_PreservesAllFields()
    {
        var original = BackupTestData.Full();

        var dto = BackupMapper.ToDto(original, createdAtMs: 42);
        var json = BackupSerializer.Serialize(dto);
        var parsed = BackupMapper.FromDto(BackupSerializer.Deserialize(json));

        Assert.Equal(2, parsed.Version);
        Assert.Empty(parsed.Warnings);

        var e = Assert.Single(parsed.Content.Entries);
        var oe = original.Entries[0];
        Assert.Equal(oe.SourceText, e.SourceText);
        Assert.Equal(oe.TargetText, e.TargetText);
        Assert.Equal(oe.AcceptedAnswers, e.AcceptedAnswers);
        Assert.Equal(oe.Tags, e.Tags);
        Assert.Equal(oe.Difficulty, e.Difficulty);
        Assert.Equal(oe.BoxLevel, e.BoxLevel);
        Assert.Equal(oe.LastIntervalDays, e.LastIntervalDays);
        Assert.Equal(oe.IsLeech, e.IsLeech);
        Assert.Equal(oe.Pronunciation, e.Pronunciation);

        var s = Assert.Single(parsed.Content.Sets);
        Assert.Equal(original.Sets[0].Title, s.Title);

        var rec = Assert.Single(parsed.Content.Sessions);
        Assert.Equal("FLASHCARD", rec.LearningMode);
        Assert.Equal(5, rec.SetId);

        Assert.NotNull(parsed.Content.Settings);
        Assert.Equal(SrsMode.FlashcardBox, parsed.Content.Settings!.Mode);
        Assert.Equal(LearningDirection.TargetToSource, parsed.Content.Settings.LearningDirection);
        Assert.Equal(new[] { 0, 4, 7, 14, 30, 180 }, parsed.Content.Settings.BoxIntervals);
    }

    [Fact]
    public void Export_OmitsNullNullables()
    {
        var content = new BackupContent(
            Sets: [new VocabularySet { Id = 1, Title = "S" }],
            Entries: [new VocabularyEntry { Id = 1, SetId = 1, SourceText = "a", TargetText = "b", LastIntervalDays = null }],
            Sessions: [new SessionRecord { Id = 1, SetId = null, SetName = "S", StartedAt = 0 }],
            Settings: null);

        var json = BackupSerializer.Serialize(BackupMapper.ToDto(content, 0));

        Assert.DoesNotContain("lastIntervalDays", json);   // int? null → weggelassen
        Assert.DoesNotContain("srsSettings", json);        // null → weggelassen
        // setId der Session ist null → im Session-Objekt weggelassen (aber Entry.setId bleibt, non-null)
        Assert.DoesNotContain("\"setId\": null", json);
    }

    [Fact]
    public void Export_AlwaysWritesNonNullStringFields()
    {
        // Leere Strings müssen geschrieben werden (sonst Android: null in non-null Kotlin-String → Crash).
        var content = new BackupContent(
            Sets: [new VocabularySet { Id = 1, Title = "S" }],
            Entries: [new VocabularyEntry { Id = 1, SetId = 1, SourceText = "a", TargetText = "b", Notes = "" }],
            Sessions: [],
            Settings: null);

        var json = BackupSerializer.Serialize(BackupMapper.ToDto(content, 0));
        Assert.Contains("\"notes\": \"\"", json);
        Assert.Contains("\"acceptedAnswers\": []", json);
    }

    [Fact]
    public void SparseGsonJson_ParsesWithDefaults()
    {
        // Simuliert Android-Gson-Output (serializeNulls aus): lastIntervalDays / setId / srsSettings fehlen.
        const string json = """
        {
          "version": 2,
          "createdAt": 123,
          "sets": [{"id":1,"title":"S","description":"","sourceLanguage":"","targetLanguage":"","createdAt":0,"updatedAt":0}],
          "entries": [{"id":1,"setId":1,"sourceText":"a","targetText":"b","acceptedAnswers":[],"exampleSentence":"","notes":"","partOfSpeech":"","gender":"","pluralForm":"","verbForms":"","pronunciation":"","tags":[],"mnemonic":"","imagePath":"","audioPath":"","difficulty":250,"boxLevel":1,"nextReviewAt":0,"correctCount":0,"wrongCount":0,"lastReviewedAt":0,"createdAt":0,"updatedAt":0,"isArchived":false,"isLeech":false}],
          "sessionRecords": [{"id":1,"setName":"S","correctCount":1,"wrongCount":0,"startedAt":0,"wrongEntryIds":"","durationMinutes":0,"learningMode":"FREE_TEXT"}]
        }
        """;

        var parsed = BackupMapper.FromDto(BackupSerializer.Deserialize(json));

        Assert.Null(parsed.Content.Entries[0].LastIntervalDays);
        Assert.Null(parsed.Content.Sessions[0].SetId);
        Assert.Null(parsed.Content.Settings);
        Assert.Empty(parsed.Content.Entries[0].AcceptedAnswers);
        Assert.Equal(250, parsed.Content.Entries[0].Difficulty);
        Assert.Empty(parsed.Warnings);
    }

    [Fact]
    public void UnknownEnum_FallsBackToDefault_WithWarning()
    {
        const string json = """
        {
          "version": 2, "createdAt": 0, "sets": [], "entries": [], "sessionRecords": [],
          "srsSettings": {"mode":"WEIRD","boxIntervals":[1,2],"strictAccents":false,"typoToleranceEnabled":true,"leechThreshold":4,"learningDirection":"???","maxCardsPerSession":50,"maxNewCardsPerDay":0}
        }
        """;

        var parsed = BackupMapper.FromDto(BackupSerializer.Deserialize(json));

        Assert.Equal(SrsMode.Adaptive, parsed.Content.Settings!.Mode);
        Assert.Equal(LearningDirection.SourceToTarget, parsed.Content.Settings.LearningDirection);
        Assert.Equal(2, parsed.Warnings.Count);   // je eine Warnung pro unbekanntem Enum
    }

    [Fact]
    public void NewerVersion_ProducesWarning_ButStillParses()
    {
        const string json = """
        { "version": 3, "createdAt": 0, "sets": [], "entries": [], "sessionRecords": [] }
        """;

        var parsed = BackupMapper.FromDto(BackupSerializer.Deserialize(json));

        Assert.Equal(3, parsed.Version);
        Assert.Single(parsed.Warnings);
    }

    [Fact]
    public void MalformedJson_ThrowsBackupFormatException()
    {
        Assert.Throws<BackupFormatException>(() => BackupSerializer.Deserialize("{ not valid json "));
    }
}
