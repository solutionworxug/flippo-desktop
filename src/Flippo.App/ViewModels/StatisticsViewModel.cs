using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Core.Statistics;
using Flippo.Data.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Flippo.App.ViewModels;

/// <summary>Ein Balken (horizontal oder vertikal): Label, Rohwert, gerenderte Pixel-Größe.</summary>
public sealed record Bar(string Label, int Value, double Size);

/// <summary>Vertikaler Karteikasten-Balken: Label, Wert, Pixel-Höhe, feste Datenfarbe.</summary>
public sealed record BoxBar(string Label, int Value, double Height, IBrush Fill);

/// <summary>Modus-Statistik in Anzeige-Form (lokalisierter Modusname + Erfolgs-Prozent).</summary>
public sealed record ModeStatDisplay(string ModeName, int SessionCount, int TotalCorrect, int TotalWrong, string SuccessText);

/// <summary>Heatmap-Zelle: Intensität 0..1 (0 = kein Lernen), Tooltip „dd.MM.yyyy: n".</summary>
public sealed record HeatCell(double Intensity, string? Tooltip);

/// <summary>Eine Heatmap-Spalte = Kalenderwoche (Mo–So, 7 Zellen).</summary>
public sealed record HeatWeek(IReadOnlyList<HeatCell> Days);

/// <summary>
/// Statistik-Screen (Port von StatisticsViewModel/DetailedStatistics). Lädt alle Karten + Sessions,
/// berechnet über <see cref="StatisticsCalculator"/> und bereitet die Balken für die handgezeichneten
/// Charts (Karteikasten, Aktivitäts-Heatmap, Wochentag, Tageszeit) auf.
/// </summary>
public sealed partial class StatisticsViewModel : ViewModelBase, IActivatable
{
    private const double MaxBarWidth = 200;
    private const double MaxBarHeight = 120;

    private readonly VocabularyStore _store;
    private readonly SessionStore _sessions;
    private readonly NavigationService _nav;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _hasModeStats;
    [ObservableProperty] private bool _hasHardestCards;
    [ObservableProperty] private LearningStatistics? _stats;

    [ObservableProperty] private string _successRateText = "";
    [ObservableProperty] private string _totalLearnedText = "";
    [ObservableProperty] private string _reviewsText = "";
    [ObservableProperty] private string _learningTimeText = "";
    [ObservableProperty] private string _avgSessionText = "";
    [ObservableProperty] private string _streakText = "";

    [ObservableProperty] private ISeries[] _progressSeries = [];
    [ObservableProperty] private Axis[] _progressXAxes = [];
    [ObservableProperty] private Axis[] _progressYAxes = [];
    [ObservableProperty] private bool _hasProgress;

    public ObservableCollection<BoxBar> BoxBars { get; } = new();
    public ObservableCollection<HeatWeek> HeatWeeks { get; } = new();
    public ObservableCollection<Bar> WeekdayBars { get; } = new();
    public ObservableCollection<Bar> HourBars { get; } = new();
    public ObservableCollection<HardCard> HardestCards { get; } = new();
    public ObservableCollection<ModeStatDisplay> ModeStats { get; } = new();

    public StatisticsViewModel(VocabularyStore store, SessionStore sessions, NavigationService nav)
    {
        _store = store;
        _sessions = sessions;
        _nav = nav;
    }

    public Task OnActivatedAsync() => LoadAsync();

    [RelayCommand] private void GoHistory() => _nav.NavigateTo<HistoryViewModel>();

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
            TotalLearnedText = (s.TotalCorrect + s.TotalWrong).ToString("N0", CultureInfo.CurrentUICulture);
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
        // Fortschrittskurve (LiveCharts2): kumulierte Karten über die Zeit
        HasProgress = s.CumulativeLearned.Count >= 2;
        var accent = new SKColor(0x25, 0x63, 0xEB);
        var axisText = new SolidColorPaint(new SKColor(0x5B, 0x65, 0x77));
        ProgressSeries =
        [
            new LineSeries<DateTimePoint>
            {
                Values = s.CumulativeLearned
                    .Select(d => new DateTimePoint(d.Date.ToDateTime(TimeOnly.MinValue), d.Count))
                    .ToArray(),
                GeometrySize = 7,
                LineSmoothness = 0.2,
                Stroke = new SolidColorPaint(accent, 2.5f),
                GeometryStroke = new SolidColorPaint(accent, 2f),
                GeometryFill = new SolidColorPaint(SKColors.White),
                Fill = new SolidColorPaint(accent.WithAlpha(0x22))
            }
        ];
        ProgressXAxes = [new DateTimeAxis(TimeSpan.FromDays(1), d => d.ToString("dd.MM")) { LabelsPaint = axisText }];
        ProgressYAxes = [new Axis { MinLimit = 0, LabelsPaint = axisText }];

        // Karteikasten (vertikal, feste Datenfarben je Fach — Stitch)
        BoxBars.Clear();
        string[] boxHex = ["#EF4444", "#F97316", "#FACC15", "#38BDF8", "#2563EB", "#16A34A"];
        int maxBox = Math.Max(1, s.CardsByBox.Count == 0 ? 1 : s.CardsByBox.Max(b => b.Count));
        foreach (var b in s.CardsByBox)
        {
            var fill = new SolidColorBrush(Color.Parse(boxHex[(b.Box - 1) % boxHex.Length]));
            BoxBars.Add(new BoxBar(string.Format(L.T("Stats_BoxLabel"), b.Box), b.Count,
                Math.Max(6, (double)b.Count / maxBox * MaxBarHeight), fill));
        }

        // Aktivitäts-Heatmap: 26 Wochen-Spalten à 7 Tage (Mo–So), Intensität relativ zum Maximum
        HeatWeeks.Clear();
        var today = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(nowMs).ToLocalTime().DateTime);
        var heatByDay = s.ActivityLast182Days.ToDictionary(d => d.Date, d => d.Count);
        int maxHeat = Math.Max(1, heatByDay.Count == 0 ? 1 : heatByDay.Values.Max());
        var heatStart = today.AddDays(-181);
        heatStart = heatStart.AddDays(-(((int)heatStart.DayOfWeek + 6) % 7));   // auf Montag zurückrunden
        for (var weekStart = heatStart; weekStart <= today; weekStart = weekStart.AddDays(7))
        {
            var cells = new List<HeatCell>(7);
            for (int d = 0; d < 7; d++)
            {
                var date = weekStart.AddDays(d);
                if (date > today) { cells.Add(new HeatCell(0, null)); continue; }
                int count = heatByDay.TryGetValue(date, out var c) ? c : 0;
                double intensity = count == 0 ? 0 : 0.25 + 0.75 * count / maxHeat;
                cells.Add(new HeatCell(intensity, $"{date:dd.MM.yyyy}: {count}"));
            }
            HeatWeeks.Add(new HeatWeek(cells));
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
