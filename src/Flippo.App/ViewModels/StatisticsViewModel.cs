using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Core.Statistics;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>Ein Balken (horizontal oder vertikal): Label, Rohwert, gerenderte Pixel-Größe.</summary>
public sealed record Bar(string Label, int Value, double Size);

/// <summary>Modus-Statistik in Anzeige-Form (lokalisierter Modusname + Erfolgs-Prozent).</summary>
public sealed record ModeStatDisplay(string ModeName, int SessionCount, int TotalCorrect, int TotalWrong, string SuccessText);

/// <summary>
/// Statistik-Screen (Port von StatisticsViewModel/DetailedStatistics). Lädt alle Karten + Sessions,
/// berechnet über <see cref="StatisticsCalculator"/> und bereitet die Balken für die handgezeichneten
/// Charts (Karteikasten, 30-Tage-Aktivität, Wochentag, Tageszeit) auf.
/// </summary>
public sealed partial class StatisticsViewModel : ViewModelBase, IActivatable
{
    private const double MaxBarWidth = 200;
    private const double MaxBarHeight = 120;

    private readonly VocabularyStore _store;
    private readonly SessionStore _sessions;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _hasModeStats;
    [ObservableProperty] private bool _hasHardestCards;
    [ObservableProperty] private LearningStatistics? _stats;

    [ObservableProperty] private string _successRateText = "";
    [ObservableProperty] private string _reviewsText = "";
    [ObservableProperty] private string _learningTimeText = "";
    [ObservableProperty] private string _avgSessionText = "";
    [ObservableProperty] private string _streakText = "";

    public ObservableCollection<Bar> BoxBars { get; } = new();
    public ObservableCollection<Bar> ActivityBars { get; } = new();
    public ObservableCollection<Bar> WeekdayBars { get; } = new();
    public ObservableCollection<Bar> HourBars { get; } = new();
    public ObservableCollection<HardCard> HardestCards { get; } = new();
    public ObservableCollection<ModeStatDisplay> ModeStats { get; } = new();

    public StatisticsViewModel(VocabularyStore store, SessionStore sessions)
    {
        _store = store;
        _sessions = sessions;
    }

    public Task OnActivatedAsync() => LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var entries = await _store.GetAllEntriesAsync();
            var records = await _sessions.GetAllAsync();
            IsEmpty = entries.Count == 0 && records.Count == 0;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var s = StatisticsCalculator.Compute(entries, records, now, TimeZoneInfo.Local);
            Stats = s;

            SuccessRateText = $"{Math.Round(s.OverallSuccessRate * 100)} %";
            ReviewsText = string.Format(L.T("Stats_ReviewsFormat"), s.TotalCorrect, s.TotalWrong);
            LearningTimeText = FormatMinutes(s.TotalLearningMinutes);
            AvgSessionText = string.Format(L.T("Stats_AvgSessionFormat"),
                s.AvgCardsPerSession, FormatMinutes((int)Math.Round(s.AvgSessionMinutes)));
            StreakText = string.Format(L.T("Stats_StreakFormat"), s.StreakDays, s.BestStreak);

            BuildBars(s, now);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildBars(LearningStatistics s, long nowMs)
    {
        // Karteikasten (horizontal)
        BoxBars.Clear();
        int maxBox = Math.Max(1, s.CardsByBox.Count == 0 ? 1 : s.CardsByBox.Max(b => b.Count));
        foreach (var b in s.CardsByBox)
            BoxBars.Add(new Bar(string.Format(L.T("Stats_BoxLabel"), b.Box), b.Count, (double)b.Count / maxBox * MaxBarWidth));

        // 30-Tage-Aktivität (vertikal, kontinuierlich alt→neu)
        ActivityBars.Clear();
        var today = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(nowMs).ToLocalTime().DateTime);
        var byDay = s.ActivityLast30Days.ToDictionary(d => d.Date, d => d.Count);
        int maxAct = Math.Max(1, byDay.Count == 0 ? 1 : byDay.Values.Max());
        for (int i = 29; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            int count = byDay.TryGetValue(date, out var c) ? c : 0;
            string label = (i == 29 || i == 15 || i == 0) ? date.ToString("dd.MM") : "";
            ActivityBars.Add(new Bar(label, count, (double)count / maxAct * MaxBarHeight));
        }

        // Wochentag (horizontal, Mo→So), Lernminuten — Namen aus der aktuellen Kultur
        WeekdayBars.Clear();
        var dayNames = CultureInfo.CurrentUICulture.DateTimeFormat.AbbreviatedDayNames;
        int maxWd = Math.Max(1, s.WeekdayMinutes.Count == 0 ? 1 : s.WeekdayMinutes.Max());
        for (int i = 0; i < s.WeekdayMinutes.Count; i++)
            WeekdayBars.Add(new Bar(dayNames[(i + 1) % 7], s.WeekdayMinutes[i], (double)s.WeekdayMinutes[i] / maxWd * MaxBarWidth));

        // Tageszeit (vertikal, 0–23)
        HourBars.Clear();
        int maxHr = Math.Max(1, s.HourlyCards.Count == 0 ? 1 : s.HourlyCards.Max());
        for (int h = 0; h < s.HourlyCards.Count; h++)
        {
            string label = h % 6 == 0 ? h.ToString(CultureInfo.InvariantCulture) : "";
            HourBars.Add(new Bar(label, s.HourlyCards[h], (double)s.HourlyCards[h] / maxHr * MaxBarHeight));
        }

        HardestCards.Clear();
        foreach (var h in s.HardestCards) HardestCards.Add(h);
        HasHardestCards = HardestCards.Count > 0;

        ModeStats.Clear();
        foreach (var m in s.ModeStats)
            ModeStats.Add(new ModeStatDisplay(
                ModeName(m.LearningMode), m.SessionCount, m.TotalCorrect, m.TotalWrong,
                $"{Math.Round(m.SuccessRate * 100)} %"));
        HasModeStats = ModeStats.Count > 0;
    }

    private static string ModeName(string mode) => mode switch
    {
        "FLASHCARD" => L.T("Sets_ModeFlashcard"),
        "FREE_TEXT" => L.T("Sets_ModeFreeText"),
        "MULTIPLE_CHOICE" => L.T("Sets_ModeMultipleChoice"),
        _ => mode
    };

    private static string FormatMinutes(int minutes)
    {
        if (minutes <= 0) return "0 min";
        int h = minutes / 60, m = minutes % 60;
        return h > 0 ? $"{h} h {m} min" : $"{m} min";
    }
}
