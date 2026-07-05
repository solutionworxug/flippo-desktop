using Flippo.Core.Domain;
using Flippo.Core.Srs;

namespace Flippo.Tests.Srs;

/// <summary>Port von SrsEngineTest.kt (17 Tests). now ist fix injiziert statt Wall-Clock.</summary>
public class SrsEngineTests
{
    private const long Now = 1_700_000_000_000L;
    private const long MillisPerDay = 86_400_000L;
    private static readonly IReadOnlyList<int> DefaultBoxIntervals = new[] { 0, 4, 7, 14, 30, 180 };

    private static VocabularyEntry MakeEntry(
        long id = 1L, int boxLevel = 1, int correctCount = 0, int wrongCount = 0, int difficulty = 250)
        => new()
        {
            Id = id,
            SetId = 1L,
            SourceText = "test",
            TargetText = "test",
            BoxLevel = boxLevel,
            CorrectCount = correctCount,
            WrongCount = wrongCount,
            Difficulty = difficulty
        };

    // ---- Karteikasten Tests ----

    [Fact] // flashcard box - correct answer advances to next box
    public void FlashcardBox_CorrectAnswerAdvancesToNextBox()
    {
        var entry = MakeEntry(boxLevel: 1);
        var update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Good, DefaultBoxIntervals, Now);
        Assert.Equal(2, update.BoxLevel);
    }

    [Fact] // flashcard box - wrong answer resets to box 1
    public void FlashcardBox_WrongAnswerResetsToBox1()
    {
        var entry = MakeEntry(boxLevel: 4);
        var update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Wrong, DefaultBoxIntervals, Now);
        Assert.Equal(1, update.BoxLevel);
    }

    [Fact] // flashcard box - correct from box 6 stays at box 6 (max)
    public void FlashcardBox_CorrectFromBox6StaysAtBox6()
    {
        var entry = MakeEntry(boxLevel: 6);
        var update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Good, DefaultBoxIntervals, Now);
        Assert.Equal(6, update.BoxLevel);
    }

    [Fact] // flashcard box - box 1 interval is 0 days
    public void FlashcardBox_Box1IntervalIs0Days()
    {
        var entry = MakeEntry(boxLevel: 1);
        var update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Wrong, DefaultBoxIntervals, Now);
        // Reset auf Box 1 (Intervall 0 Tage) → nextReview == now.
        Assert.Equal(Now, update.NextReviewAt);
    }

    [Fact] // flashcard box - box 2 interval is 4 days
    public void FlashcardBox_Box2IntervalIs4Days()
    {
        var entry = MakeEntry(boxLevel: 1);
        var update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Good, DefaultBoxIntervals, Now);
        Assert.Equal(Now + 4 * MillisPerDay, update.NextReviewAt);
    }

    [Fact] // flashcard box - correct count increments on correct
    public void FlashcardBox_CorrectCountIncrementsOnCorrect()
    {
        var entry = MakeEntry(correctCount: 5);
        var update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Good, DefaultBoxIntervals, Now);
        Assert.Equal(6, update.CorrectCount);
        Assert.Equal(0, update.WrongCount);
    }

    [Fact] // flashcard box - wrong count increments on wrong
    public void FlashcardBox_WrongCountIncrementsOnWrong()
    {
        var entry = MakeEntry(wrongCount: 2);
        var update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Wrong, DefaultBoxIntervals, Now);
        Assert.Equal(3, update.WrongCount);
        Assert.Equal(0, update.CorrectCount);
    }

    [Fact] // flashcard box - leech triggered after 4 wrong answers
    public void FlashcardBox_LeechTriggeredAfter4WrongAnswers()
    {
        var entry = MakeEntry(wrongCount: 3);
        var update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Wrong, DefaultBoxIntervals, Now);
        Assert.True(update.IsLeech);
    }

    [Fact] // flashcard box - not leech with 3 wrong answers
    public void FlashcardBox_NotLeechWith3WrongAnswers()
    {
        var entry = MakeEntry(wrongCount: 2);
        var update = SrsEngine.ScheduleFlashcardBox(entry, ReviewResult.Wrong, DefaultBoxIntervals, Now);
        Assert.False(update.IsLeech);
    }

    [Fact] // flashcard box - EASY and HARD treated as correct
    public void FlashcardBox_EasyAndHardTreatedAsCorrect()
    {
        var entryEasy = MakeEntry(boxLevel: 2);
        var entryHard = MakeEntry(boxLevel: 2);

        var updateEasy = SrsEngine.ScheduleFlashcardBox(entryEasy, ReviewResult.Easy, DefaultBoxIntervals, Now);
        var updateHard = SrsEngine.ScheduleFlashcardBox(entryHard, ReviewResult.Hard, DefaultBoxIntervals, Now);

        Assert.Equal(3, updateEasy.BoxLevel);
        Assert.Equal(3, updateHard.BoxLevel);
    }

    // ---- Adaptive Tests ----

    [Fact] // adaptive - correct answer increases repetition count
    public void Adaptive_CorrectAnswerIncreasesRepetitionCount()
    {
        var entry = MakeEntry(boxLevel: 0, difficulty: 250);
        var update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Good, Now);
        Assert.True(update.BoxLevel > 0);
    }

    [Fact] // adaptive - wrong answer resets repetitions
    public void Adaptive_WrongAnswerResetsRepetitions()
    {
        var entry = MakeEntry(boxLevel: 5, difficulty: 250);
        var update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Wrong, Now);
        Assert.Equal(0, update.BoxLevel);
    }

    [Fact] // adaptive - easy answer boosts ease factor
    public void Adaptive_EasyAnswerBoostsEaseFactor()
    {
        var entry = MakeEntry(boxLevel: 2, difficulty: 250);
        var update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Easy, Now);
        Assert.True(update.Difficulty > 250);
    }

    [Fact] // adaptive - hard answer reduces ease factor
    public void Adaptive_HardAnswerReducesEaseFactor()
    {
        var entry = MakeEntry(boxLevel: 2, difficulty: 250);
        var update = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Hard, Now);
        Assert.True(update.Difficulty < 250);
    }

    [Fact] // adaptive - ease factor never drops below 130
    public void Adaptive_EaseFactorNeverDropsBelow130()
    {
        var entry = MakeEntry(boxLevel: 2, difficulty: 135);
        for (int i = 0; i < 10; i++)
        {
            var upd = SrsEngine.ScheduleAdaptive(entry, ReviewResult.Wrong, Now);
            entry = entry with { Difficulty = upd.Difficulty, BoxLevel = upd.BoxLevel };
        }
        Assert.True(entry.Difficulty >= 130);
    }

    // ---- schedule() dispatcher test ----

    [Fact] // schedule dispatches to flashcard box when mode is FLASHCARD_BOX
    public void Schedule_DispatchesToFlashcardBox()
    {
        var entry = MakeEntry(boxLevel: 2);
        var settings = new SrsSettings { Mode = SrsMode.FlashcardBox, BoxIntervals = DefaultBoxIntervals };
        var update = SrsEngine.Schedule(entry, ReviewResult.Wrong, settings, Now);
        Assert.Equal(1, update.BoxLevel); // reset to box 1
    }

    [Fact] // schedule dispatches to adaptive when mode is ADAPTIVE
    public void Schedule_DispatchesToAdaptive()
    {
        var entry = MakeEntry(boxLevel: 5);
        var settings = new SrsSettings { Mode = SrsMode.Adaptive };
        var update = SrsEngine.Schedule(entry, ReviewResult.Wrong, settings, Now);
        Assert.Equal(0, update.BoxLevel); // adaptive resets to 0
    }
}
