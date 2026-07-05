using Flippo.Core.Domain;
using Flippo.Core.Session;

namespace Flippo.Tests.Session;

/// <summary>
/// Aufbau des SessionRecord am Session-Ende (Plan 1.4). Interop-kritisch: LearningMode-String
/// (FLASHCARD/FREE_TEXT/MULTIPLE_CHOICE), durationMinutes = max(1, elapsed/60000), wrongEntryIds als CSV.
/// </summary>
public class SessionResultTests
{
    [Fact] // nichts beantwortet → kein Record (Android: nur wenn correct+wrong > 0)
    public void Build_ReturnsNull_WhenNothingAnswered()
    {
        var record = SessionResult.Build(
            setId: 1, setName: "Set", mode: LearningMode.Flashcard,
            correctCount: 0, wrongCount: 0, wrongEntryIds: [],
            startedAtMs: 0, nowMs: 60_000);

        Assert.Null(record);
    }

    [Fact] // vollständiger Record: Felder durchgereicht, Mode-String, CSV, Dauer
    public void Build_ProducesRecord_WithModeStringAndCsv()
    {
        var record = SessionResult.Build(
            setId: 7, setName: "Spanisch", mode: LearningMode.Flashcard,
            correctCount: 4, wrongCount: 2, wrongEntryIds: [11, 22],
            startedAtMs: 1000, nowMs: 1000 + 150_000);   // 150 s Dauer

        Assert.NotNull(record);
        Assert.Equal(7, record!.SetId);
        Assert.Equal("Spanisch", record.SetName);
        Assert.Equal("FLASHCARD", record.LearningMode);
        Assert.Equal(4, record.CorrectCount);
        Assert.Equal(2, record.WrongCount);
        Assert.Equal("11,22", record.WrongEntryIds);
        Assert.Equal(1000, record.StartedAt);
        Assert.Equal(2, record.DurationMinutes);   // 150 s / 60 = 2
    }

    [Fact] // sehr kurze Session → mindestens 1 Minute; keine Falschen → leeres CSV; "Alle" → SetId null
    public void Build_DurationIsAtLeastOneMinute()
    {
        var record = SessionResult.Build(
            setId: null, setName: "Alle", mode: LearningMode.Flashcard,
            correctCount: 1, wrongCount: 0, wrongEntryIds: [],
            startedAtMs: 0, nowMs: 5_000);   // 5 s

        Assert.Equal(1, record!.DurationMinutes);
        Assert.Equal("", record.WrongEntryIds);
        Assert.Null(record.SetId);
    }

    [Theory] // Mode → exakter Interop-String
    [InlineData(LearningMode.Flashcard, "FLASHCARD")]
    [InlineData(LearningMode.FreeText, "FREE_TEXT")]
    [InlineData(LearningMode.MultipleChoice, "MULTIPLE_CHOICE")]
    public void Build_MapsModeToBackupString(LearningMode mode, string expected)
    {
        var record = SessionResult.Build(null, "S", mode, 1, 0, [], 0, 60_000);

        Assert.Equal(expected, record!.LearningMode);
    }
}
