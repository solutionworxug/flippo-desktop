using Flippo.Core.Domain;

namespace Flippo.Core.Statistics;

/// <summary>
/// Port von GetStatisticsUseCase.getDetailed (+ der DAO-Aggregate). Reine In-Memory-Berechnung aus
/// allen Karten + SessionRecords; <paramref name="nowMs"/> und <paramref name="timeZone"/> injiziert
/// (deterministisch, testbar). Android nutzt SQLite <c>DATE(..,'localtime')</c> — hier die entsprechende
/// lokale Kalendertag-Zuordnung über die übergebene Zeitzone.
/// </summary>
public static class StatisticsCalculator
{
    public static LearningStatistics Compute(
        IReadOnlyList<VocabularyEntry> entries,
        IReadOnlyList<SessionRecord> sessions,
        long nowMs,
        TimeZoneInfo timeZone)
    {
        // ── Karten ──
        int totalReviews = entries.Sum(e => e.CorrectCount + e.WrongCount);
        int totalCorrect = entries.Sum(e => e.CorrectCount);
        float overallSuccessRate = totalReviews == 0 ? 0f : (float)totalCorrect / totalReviews;

        var active = entries.Where(e => !e.IsArchived).ToList();
        int newCards = active.Count(e => e.IsNew);
        int dueAll = entries.Count(e => e.IsDue(nowMs));
        int dueToday = Math.Max(0, dueAll - newCards);
        int leechCards = active.Count(e => e.IsLeech);

        var cardsByBox = active
            .GroupBy(e => e.BoxLevel)
            .OrderBy(g => g.Key)
            .Select(g => new BoxCount(g.Key, g.Count()))
            .ToList();

        var hardestCards = entries
            .Where(e => e.WrongCount > 0)
            .OrderByDescending(e => e.WrongCount)
            .Take(10)
            .Select(e => new HardCard(e.Id, e.SourceText, e.TargetText, e.WrongCount, e.SuccessRate))
            .ToList();

        // ── Sessions ──
        int sessionCount = sessions.Count;
        int totalLearningMinutes = sessions.Sum(s => s.DurationMinutes);
        float avgCardsPerSession = sessions.Count == 0 ? 0f : (float)sessions.Sum(s => s.Total) / sessions.Count;
        var withDuration = sessions.Where(s => s.DurationMinutes > 0).ToList();
        float avgSessionMinutes = withDuration.Count == 0 ? 0f
            : (float)withDuration.Sum(s => s.DurationMinutes) / withDuration.Count;

        DateOnly DayOf(long ms) => DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime, timeZone));
        DateTime LocalOf(long ms) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime, timeZone);

        // Streak: distinkte Lern-Tage, absteigend
        var distinctDaysDesc = sessions
            .Where(s => s.StartedAt > 0)
            .Select(s => DayOf(s.StartedAt))
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();
        int streakDays = CurrentStreak(distinctDaysDesc);
        int bestStreak = BestStreak(distinctDaysDesc);

        // Aktivität der letzten 30 Tage (Summe correct+wrong je Tag)
        var today = DayOf(nowMs);
        var since30 = today.AddDays(-30);
        var activity30 = sessions
            .Where(s => s.StartedAt > 0 && DayOf(s.StartedAt) > since30)
            .GroupBy(s => DayOf(s.StartedAt))
            .Select(g => new DayCount(g.Key, g.Sum(s => s.Total)))
            .OrderBy(d => d.Date)
            .ToList();

        int thisWeekCards = activity30.Where(d => d.Date > today.AddDays(-7)).Sum(d => d.Count);
        int lastWeekCards = activity30
            .Where(d => d.Date <= today.AddDays(-7) && d.Date > today.AddDays(-14))
            .Sum(d => d.Count);

        // Fortschrittskurve: kumulierte Karten je Lern-Tag (gesamte Historie)
        var cumulative = new List<DayCount>();
        int running = 0;
        foreach (var g in sessions.Where(s => s.StartedAt > 0)
                     .GroupBy(s => DayOf(s.StartedAt)).OrderBy(g => g.Key))
        {
            running += g.Sum(s => s.Total);
            cumulative.Add(new DayCount(g.Key, running));
        }

        // Heatmap: Aktivität der letzten 182 Tage (26 Wochen)
        var since182 = today.AddDays(-182);
        var activity182 = sessions
            .Where(s => s.StartedAt > 0 && DayOf(s.StartedAt) > since182)
            .GroupBy(s => DayOf(s.StartedAt))
            .Select(g => new DayCount(g.Key, g.Sum(s => s.Total)))
            .OrderBy(d => d.Date)
            .ToList();

        // Wochentag (Mo=0 … So=6) → Lernminuten
        var weekdayMinutes = new int[7];
        foreach (var s in sessions.Where(s => s.StartedAt > 0))
        {
            int idx = ((int)LocalOf(s.StartedAt).DayOfWeek + 6) % 7;   // Sonntag(0)→6, Montag(1)→0
            weekdayMinutes[idx] += s.DurationMinutes;
        }

        // Tageszeit (0…23) → Karten
        var hourlyCards = new int[24];
        foreach (var s in sessions.Where(s => s.StartedAt > 0))
            hourlyCards[LocalOf(s.StartedAt).Hour] += s.Total;

        var modeStats = sessions
            .GroupBy(s => s.LearningMode)
            .Select(g => new ModeStat(g.Key, g.Count(), g.Sum(s => s.CorrectCount), g.Sum(s => s.WrongCount)))
            .OrderByDescending(m => m.SessionCount)
            .ToList();

        return new LearningStatistics
        {
            TotalCards = entries.Count,
            DueToday = dueToday,
            NewCards = newCards,
            LeechCards = leechCards,
            TotalCorrect = totalCorrect,
            TotalWrong = totalReviews - totalCorrect,
            OverallSuccessRate = overallSuccessRate,
            CardsByBox = cardsByBox,
            HardestCards = hardestCards,
            SessionCount = sessionCount,
            TotalLearningMinutes = totalLearningMinutes,
            AvgCardsPerSession = avgCardsPerSession,
            AvgSessionMinutes = avgSessionMinutes,
            StreakDays = streakDays,
            BestStreak = bestStreak,
            ActivityLast30Days = activity30,
            CumulativeLearned = cumulative,
            ActivityLast182Days = activity182,
            WeekdayMinutes = weekdayMinutes,
            HourlyCards = hourlyCards,
            ThisWeekCards = thisWeekCards,
            LastWeekCards = lastWeekCards,
            ModeStats = modeStats
        };
    }

    /// <summary>Aktuelle Serie: aufeinanderfolgende Tage ab dem jüngsten Lern-Tag (Port getStreakDays).</summary>
    private static int CurrentStreak(IReadOnlyList<DateOnly> daysDesc)
    {
        if (daysDesc.Count == 0) return 0;
        int streak = 1;
        var prev = daysDesc[0];
        for (int i = 1; i < daysDesc.Count; i++)
        {
            if (prev.DayNumber - daysDesc[i].DayNumber == 1) { streak++; prev = daysDesc[i]; }
            else break;
        }
        return streak;
    }

    /// <summary>Längste Serie überhaupt (Port getBestStreak).</summary>
    private static int BestStreak(IReadOnlyList<DateOnly> daysDesc)
    {
        if (daysDesc.Count == 0) return 0;
        int best = 1, current = 1;
        for (int i = 1; i < daysDesc.Count; i++)
        {
            if (daysDesc[i - 1].DayNumber - daysDesc[i].DayNumber == 1)
            {
                current++;
                if (current > best) best = current;
            }
            else current = 1;
        }
        return best;
    }
}
