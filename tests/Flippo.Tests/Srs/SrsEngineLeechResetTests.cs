using Flippo.Core.Domain;
using Flippo.Core.Srs;

namespace Flippo.Tests.Srs;

/// <summary>Port von SrsEngineLeechResetTest.kt (6 Tests). LEECH_RESET_STREAK = 3.</summary>
public class SrsEngineLeechResetTests
{
    private const long Now = 1_700_000_000_000L;
    private static readonly IReadOnlyList<int> DefaultBoxIntervals = new[] { 0, 4, 7, 14, 30, 180 };

    [Fact] // flashcard box - 3 richtige in folge ab box 1 setzen leech zurueck
    public void FlashcardBox_3RichtigeInFolgeAbBox1SetzenLeechZurueck()
    {
        // Pre-State: Karte ist Leech, Box 1 (gerade nach falsch zurückgesetzt)
        var entry = MakeLeech(boxLevel: 1, wrongCount: 5);

        var update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Good, DefaultBoxIntervals, Now);
        Assert.True(update.IsLeech, "Nach 1x richtig noch Leech (Box 2)");

        entry = entry with { BoxLevel = update.BoxLevel, IsLeech = update.IsLeech, CorrectCount = update.CorrectCount, WrongCount = update.WrongCount };
        update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Good, DefaultBoxIntervals, Now);
        Assert.True(update.IsLeech, "Nach 2x richtig noch Leech (Box 3)");

        entry = entry with { BoxLevel = update.BoxLevel, IsLeech = update.IsLeech, CorrectCount = update.CorrectCount, WrongCount = update.WrongCount };
        update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Good, DefaultBoxIntervals, Now);
        Assert.False(update.IsLeech, "Nach 3x richtig in Folge (Box 4) NICHT mehr Leech");
    }

    [Fact] // flashcard box - falsch zwischen 3 richtigen verhindert leech-reset
    public void FlashcardBox_FalschZwischen3RichtigenVerhindertLeechReset()
    {
        var entry = MakeLeech(boxLevel: 1, wrongCount: 5);

        var update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Good, DefaultBoxIntervals, Now);
        entry = entry with { BoxLevel = update.BoxLevel, IsLeech = update.IsLeech, CorrectCount = update.CorrectCount, WrongCount = update.WrongCount };
        update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Good, DefaultBoxIntervals, Now);
        entry = entry with { BoxLevel = update.BoxLevel, IsLeech = update.IsLeech, CorrectCount = update.CorrectCount, WrongCount = update.WrongCount };
        update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Wrong, DefaultBoxIntervals, Now);
        Assert.True(update.IsLeech, "Nach Wrong wieder Leech (oder noch immer Leech)");

        entry = entry with { BoxLevel = update.BoxLevel, IsLeech = update.IsLeech, CorrectCount = update.CorrectCount, WrongCount = update.WrongCount };
        update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Good, DefaultBoxIntervals, Now);
        Assert.True(update.IsLeech, "Nach 1x richtig nach Wrong noch Leech (Box 2)");
    }

    [Fact] // adaptive - 3 GOOD in folge setzen leech zurueck
    public void Adaptive_3GoodInFolgeSetzenLeechZurueck()
    {
        var entry = MakeLeech(boxLevel: 0, wrongCount: 5, difficulty: 250);

        var update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Good, Now);
        Assert.True(update.IsLeech, "Nach 1x GOOD noch Leech (rep=1)");

        entry = ApplyUpdate(entry, update);
        update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Good, Now);
        Assert.True(update.IsLeech, "Nach 2x GOOD noch Leech (rep=2)");

        entry = ApplyUpdate(entry, update);
        update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Good, Now);
        Assert.False(update.IsLeech, "Nach 3x GOOD in Folge (rep=3) NICHT mehr Leech");
    }

    [Fact] // adaptive - WRONG zwischendurch setzt repetitions auf 0 und verhindert reset
    public void Adaptive_WrongZwischendurchVerhindertReset()
    {
        var entry = MakeLeech(boxLevel: 0, wrongCount: 5, difficulty: 250);

        var update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Good, Now);
        entry = ApplyUpdate(entry, update);
        update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Good, Now);
        entry = ApplyUpdate(entry, update);
        update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Wrong, Now);
        entry = ApplyUpdate(entry, update);
        Assert.True(entry.IsLeech, "Nach WRONG immer noch Leech");

        update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Good, Now);
        entry = ApplyUpdate(entry, update);
        update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Good, Now);
        Assert.True(update.IsLeech, "Nach 2x GOOD nach WRONG noch Leech (rep=2)");
    }

    [Fact] // adaptive - EASY zaehlt fuer leech-reset wie GOOD
    public void Adaptive_EasyZaehltFuerLeechResetWieGood()
    {
        var entry = MakeLeech(boxLevel: 0, wrongCount: 5, difficulty: 250);

        var update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Easy, Now);
        entry = ApplyUpdate(entry, update);
        update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Easy, Now);
        entry = ApplyUpdate(entry, update);
        update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Easy, Now);
        Assert.False(update.IsLeech, "3x EASY = Leech-Reset");
    }

    [Fact] // adaptive - HARD reicht NICHT fuer leech-reset
    public void Adaptive_HardReichtNichtFuerLeechReset()
    {
        var entry = MakeLeech(boxLevel: 0, wrongCount: 5, difficulty: 250);

        var update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Hard, Now);
        entry = ApplyUpdate(entry, update);
        update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Hard, Now);
        entry = ApplyUpdate(entry, update);
        update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Hard, Now);
        Assert.True(update.IsLeech, "Nach 3x HARD noch Leech (HARD zaehlt nicht fuer Reset)");
    }

    private static VocabularyEntry MakeLeech(int boxLevel, int wrongCount, int difficulty = 0)
        => new()
        {
            Id = 1L,
            SetId = 1L,
            SourceText = "test",
            TargetText = "test",
            BoxLevel = boxLevel,
            CorrectCount = 0,
            WrongCount = wrongCount,
            Difficulty = difficulty,
            IsLeech = true
        };

    private static VocabularyEntry ApplyUpdate(VocabularyEntry entry, VocabularyEntryUpdate update)
        => entry with
        {
            BoxLevel = update.BoxLevel,
            IsLeech = update.IsLeech,
            CorrectCount = update.CorrectCount,
            WrongCount = update.WrongCount,
            Difficulty = update.Difficulty,
            LastIntervalDays = update.LastIntervalDays
        };
}
