using Flippo.Core.Domain;
using Flippo.Core.Statistics;

namespace Flippo.Tests.Statistics;

/// <summary>
/// StatisticsCalculator (Port von GetStatisticsUseCase.getDetailed). UTC-Zeitzone = deterministisch.
/// Basis-Datum 2024-01-15 ist ein Montag.
/// </summary>
public class StatisticsCalculatorTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private const long Day = 86_400_000L;

    private static long At(int year, int month, int day, int hour = 12) =>
        new DateTimeOffset(year, month, day, hour, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static VocabularyEntry Card(int correct = 0, int wrong = 0, int box = 1, bool leech = false,
        bool archived = false, long nextReview = 0, long id = 0, string src = "s", string tgt = "t") =>
        new()
        {
            Id = id, CorrectCount = correct, WrongCount = wrong, BoxLevel = box, IsLeech = leech,
            IsArchived = archived, NextReviewAt = nextReview, SourceText = src, TargetText = tgt
        };

    private static SessionRecord Sess(long startedAt, int correct = 0, int wrong = 0, int duration = 0,
        string mode = "FLASHCARD") =>
        new() { StartedAt = startedAt, CorrectCount = correct, WrongCount = wrong, DurationMinutes = duration, LearningMode = mode };

    private static LearningStatistics Compute(IReadOnlyList<VocabularyEntry> cards, IReadOnlyList<SessionRecord> sessions, long now) =>
        StatisticsCalculator.Compute(cards, sessions, now, Utc);

    [Fact]
    public void Compute_CardCounts_DueNewLeechAndSuccessRate()
    {
        long now = At(2024, 1, 15);
        var cards = new[]
        {
            Card(correct: 0, wrong: 0, nextReview: 0),                       // new + due
            Card(correct: 5, wrong: 1, nextReview: now - Day),              // due, not new
            Card(correct: 3, wrong: 0, nextReview: now + Day),             // not due
            Card(correct: 2, wrong: 8, leech: true, nextReview: now - Day), // due + leech
            Card(correct: 1, wrong: 0, archived: true, nextReview: now - Day) // archived → excluded
        };

        var s = Compute(cards, [], now);

        Assert.Equal(5, s.TotalCards);
        Assert.Equal(1, s.NewCards);
        Assert.Equal(2, s.DueToday);          // dueAll 3 − new 1
        Assert.Equal(1, s.LeechCards);
        Assert.Equal(11, s.TotalCorrect);     // 0+5+3+2+1
        Assert.Equal(9, s.TotalWrong);        // reviews 20 − correct 11
        Assert.Equal(0.55f, s.OverallSuccessRate, 3);
    }

    [Fact]
    public void Compute_CardsByBox_GroupsActiveByLevel()
    {
        var cards = new[]
        {
            Card(box: 1), Card(box: 1), Card(box: 3),
            Card(box: 3, archived: true)   // archiviert zählt nicht
        };

        var s = Compute(cards, [], At(2024, 1, 15));

        Assert.Equal(2, s.CardsByBox.Count);
        Assert.Equal(new BoxCount(1, 2), s.CardsByBox[0]);
        Assert.Equal(new BoxCount(3, 1), s.CardsByBox[1]);
    }

    [Fact]
    public void Compute_HardestCards_SortedByWrongDescTopTen()
    {
        var cards = Enumerable.Range(1, 12)
            .Select(i => Card(wrong: i, id: i))       // wrong 1..12
            .Append(Card(wrong: 0, id: 99))           // 0 falsch → ausgeschlossen
            .ToArray();

        var s = Compute(cards, [], At(2024, 1, 15));

        Assert.Equal(10, s.HardestCards.Count);
        Assert.Equal(12, s.HardestCards[0].WrongCount);   // höchster zuerst
        Assert.Equal(3, s.HardestCards[9].WrongCount);    // Top 10 → runter bis 3
    }

    [Fact]
    public void Compute_CurrentStreak_CountsConsecutiveDays()
    {
        var sessions = new[] { Sess(At(2024, 1, 15, 10)), Sess(At(2024, 1, 14, 10)), Sess(At(2024, 1, 13, 10)) };
        Assert.Equal(3, Compute([], sessions, At(2024, 1, 15)).StreakDays);
    }

    [Fact]
    public void Compute_CurrentStreak_BreaksOnGap()
    {
        var sessions = new[] { Sess(At(2024, 1, 15, 10)), Sess(At(2024, 1, 14, 10)), Sess(At(2024, 1, 11, 10)) };
        Assert.Equal(2, Compute([], sessions, At(2024, 1, 15)).StreakDays);   // 15–14, dann Lücke
    }

    [Fact]
    public void Compute_BestStreak_FindsLongestRun()
    {
        var sessions = new[]
        {
            Sess(At(2024, 1, 15, 10)), Sess(At(2024, 1, 14, 10)),                    // Lauf 2
            Sess(At(2024, 1, 10, 10)), Sess(At(2024, 1, 9, 10)), Sess(At(2024, 1, 8, 10)) // Lauf 3
        };
        var s = Compute([], sessions, At(2024, 1, 15));
        Assert.Equal(2, s.StreakDays);
        Assert.Equal(3, s.BestStreak);
    }

    [Fact]
    public void Compute_ModeStats_GroupsByLearningModeOrderedByCount()
    {
        var sessions = new[]
        {
            Sess(At(2024, 1, 15), correct: 8, wrong: 2, mode: "FLASHCARD"),
            Sess(At(2024, 1, 14), correct: 5, wrong: 5, mode: "FLASHCARD"),
            Sess(At(2024, 1, 13), correct: 9, wrong: 1, mode: "FREE_TEXT")
        };

        var s = Compute([], sessions, At(2024, 1, 15));

        Assert.Equal("FLASHCARD", s.ModeStats[0].LearningMode);
        Assert.Equal(2, s.ModeStats[0].SessionCount);
        Assert.Equal(13, s.ModeStats[0].TotalCorrect);
        Assert.Equal(7, s.ModeStats[0].TotalWrong);
        Assert.Equal("FREE_TEXT", s.ModeStats[1].LearningMode);
    }

    [Fact]
    public void Compute_WeekdayMinutes_MondayIsIndexZero()
    {
        var sessions = new[] { Sess(At(2024, 1, 15, 10), duration: 30) };   // Montag
        var s = Compute([], sessions, At(2024, 1, 15));
        Assert.Equal(30, s.WeekdayMinutes[0]);
        Assert.Equal(0, s.WeekdayMinutes[6]);
    }

    [Fact]
    public void Compute_HourlyCards_BucketsByHour()
    {
        var sessions = new[] { Sess(At(2024, 1, 15, 14), correct: 6, wrong: 4) };
        var s = Compute([], sessions, At(2024, 1, 15));
        Assert.Equal(10, s.HourlyCards[14]);
    }

    [Fact]
    public void Compute_SessionAggregates()
    {
        var sessions = new[]
        {
            Sess(At(2024, 1, 15), correct: 8, wrong: 2, duration: 10),
            Sess(At(2024, 1, 14), correct: 5, wrong: 5, duration: 20),
            Sess(At(2024, 1, 13), correct: 4, wrong: 0, duration: 0)
        };

        var s = Compute([], sessions, At(2024, 1, 15));

        Assert.Equal(3, s.SessionCount);
        Assert.Equal(30, s.TotalLearningMinutes);
        Assert.Equal(8f, s.AvgCardsPerSession, 3);       // (10+10+4)/3
        Assert.Equal(15f, s.AvgSessionMinutes, 3);       // (10+20)/2 (nur mit Dauer)
    }

    [Fact]
    public void Compute_ThisWeekVsLastWeek()
    {
        var sessions = new[]
        {
            Sess(At(2024, 1, 15), correct: 5, wrong: 0),   // heute → diese Woche
            Sess(At(2024, 1, 10), correct: 3, wrong: 0),   // 5 Tage → diese Woche
            Sess(At(2024, 1, 5), correct: 7, wrong: 0)     // 10 Tage → letzte Woche
        };

        var s = Compute([], sessions, At(2024, 1, 15));

        Assert.Equal(8, s.ThisWeekCards);
        Assert.Equal(7, s.LastWeekCards);
        Assert.Equal(3, s.ActivityLast30Days.Count);
    }

    [Fact]
    public void Compute_Empty_ReturnsZeros()
    {
        var s = Compute([], [], At(2024, 1, 15));
        Assert.Equal(0, s.TotalCards);
        Assert.Equal(0, s.StreakDays);
        Assert.Equal(0f, s.OverallSuccessRate);
        Assert.Empty(s.CardsByBox);
        Assert.Empty(s.ModeStats);
    }
}
