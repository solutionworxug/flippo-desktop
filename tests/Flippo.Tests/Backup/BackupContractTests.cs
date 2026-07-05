using System.Text.Json;
using Flippo.Core.Backup;

namespace Flippo.Tests.Backup;

/// <summary>
/// Drift-Alarm: exakte JSON-Key-Menge pro Objekt gegen hartkodierte Liste. Ändert sich das
/// Backup-Format versehentlich, schlägt genau dieser Test fehl (Plan 4.3).
/// </summary>
public class BackupContractTests
{
    private static JsonElement SerializeAndParse()
    {
        var dto = BackupMapper.ToDto(BackupTestData.Full(), createdAtMs: 1_700_000_000_000);
        var json = BackupSerializer.Serialize(dto);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static HashSet<string> Keys(JsonElement obj)
        => obj.EnumerateObject().Select(p => p.Name).ToHashSet();

    [Fact]
    public void BackupData_HasExactKeys()
    {
        var root = SerializeAndParse();
        Assert.Equal(
            new HashSet<string> { "version", "createdAt", "sets", "entries", "sessionRecords", "srsSettings" },
            Keys(root));
        Assert.Equal(2, root.GetProperty("version").GetInt32());
    }

    [Fact]
    public void Set_HasExactKeys()
    {
        var set = SerializeAndParse().GetProperty("sets")[0];
        Assert.Equal(
            new HashSet<string> { "id", "title", "description", "sourceLanguage", "targetLanguage", "createdAt", "updatedAt" },
            Keys(set));
    }

    [Fact]
    public void Entry_HasExactKeys()
    {
        var entry = SerializeAndParse().GetProperty("entries")[0];
        Assert.Equal(
            new HashSet<string>
            {
                "id", "setId", "sourceText", "targetText", "acceptedAnswers", "exampleSentence", "notes",
                "partOfSpeech", "gender", "pluralForm", "verbForms", "pronunciation", "tags", "mnemonic",
                "imagePath", "audioPath", "difficulty", "boxLevel", "nextReviewAt", "correctCount",
                "wrongCount", "lastReviewedAt", "createdAt", "updatedAt", "isArchived", "isLeech", "lastIntervalDays"
            },
            Keys(entry));
    }

    [Fact]
    public void SessionRecord_HasExactKeys_WithLearningMode()
    {
        var rec = SerializeAndParse().GetProperty("sessionRecords")[0];
        Assert.Equal(
            new HashSet<string>
            {
                "id", "setId", "setName", "correctCount", "wrongCount", "startedAt",
                "wrongEntryIds", "durationMinutes", "learningMode"
            },
            Keys(rec));
        // Domain-Feldname learningMode (NICHT learnMode)
        Assert.True(rec.TryGetProperty("learningMode", out _));
        Assert.False(rec.TryGetProperty("learnMode", out _));
    }

    [Fact]
    public void SrsSettings_HasExactKeys_EnumsAsStrings()
    {
        var s = SerializeAndParse().GetProperty("srsSettings");
        Assert.Equal(
            new HashSet<string>
            {
                "mode", "boxIntervals", "strictAccents", "typoToleranceEnabled",
                "leechThreshold", "learningDirection", "maxCardsPerSession", "maxNewCardsPerDay"
            },
            Keys(s));
        Assert.Equal("FLASHCARD_BOX", s.GetProperty("mode").GetString());
        Assert.Equal("TARGET_TO_SOURCE", s.GetProperty("learningDirection").GetString());
    }

    [Fact]
    public void Accents_AreNotUnicodeEscaped()
    {
        var dto = BackupMapper.ToDto(BackupTestData.Full(), 0);
        var json = BackupSerializer.Serialize(dto);
        // UnsafeRelaxedJsonEscaping → Akzente bleiben lesbar, nicht \u00XX
        Assert.Contains("ˈkasa", json);
        Assert.DoesNotContain("\\u", json);
    }
}
