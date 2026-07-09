namespace Flippo.Core.Statistics;

/// <summary>Aggregat pro Lernmodus (Port von ModeStats). <see cref="LearningMode"/> = Backup-String.</summary>
public sealed record ModeStat(string LearningMode, int SessionCount, int TotalCorrect, int TotalWrong)
{
    public int Total => TotalCorrect + TotalWrong;
    public float SuccessRate => Total > 0 ? (float)TotalCorrect / Total : 0f;
}

/// <summary>Eine besonders fehleranfällige Karte (Port von HardCard).</summary>
public sealed record HardCard(long Id, string SourceText, string TargetText, int WrongCount, float SuccessRate);

/// <summary>Kartenzahl je Karteikasten-Fach.</summary>
public sealed record BoxCount(int Box, int Count);

/// <summary>Gelernte Karten (correct+wrong) an einem lokalen Kalendertag.</summary>
public sealed record DayCount(DateOnly Date, int Count);

/// <summary>
/// Vollständige Lern-Statistik (Port von DetailedStatistics). Wird rein lokal berechnet und angezeigt —
/// steht in keinem Backup-Feld, daher .NET-native Datumslogik statt Byte-Parität zu Android.
/// </summary>
public sealed record LearningStatistics
{
    // Karten
    public int TotalCards { get; init; }
    public int DueToday { get; init; }          // fällig, ohne neue Karten
    public int NewCards { get; init; }
    public int LeechCards { get; init; }
    public int TotalCorrect { get; init; }
    public int TotalWrong { get; init; }
    public float OverallSuccessRate { get; init; }
    public IReadOnlyList<BoxCount> CardsByBox { get; init; } = [];
    public IReadOnlyList<HardCard> HardestCards { get; init; } = [];

    // Sessions
    public int SessionCount { get; init; }
    public int TotalLearningMinutes { get; init; }
    public float AvgCardsPerSession { get; init; }
    public float AvgSessionMinutes { get; init; }
    public int StreakDays { get; init; }
    public int BestStreak { get; init; }

    // Zeitreihen / Charts
    public IReadOnlyList<DayCount> ActivityLast30Days { get; init; } = [];
    /// <summary>Kumulierte Kartensumme (correct+wrong) je aktivem Lern-Tag, aufsteigend — Fortschrittskurve.</summary>
    public IReadOnlyList<DayCount> CumulativeLearned { get; init; } = [];
    /// <summary>Aktivität der letzten 182 Tage (26 Wochen) — Heatmap.</summary>
    public IReadOnlyList<DayCount> ActivityLast182Days { get; init; } = [];
    public IReadOnlyList<int> WeekdayMinutes { get; init; } = [];   // Index 0=Montag … 6=Sonntag, Lernminuten
    public IReadOnlyList<int> HourlyCards { get; init; } = [];      // Index 0…23, Karten
    public int ThisWeekCards { get; init; }
    public int LastWeekCards { get; init; }
    public IReadOnlyList<ModeStat> ModeStats { get; init; } = [];
}
