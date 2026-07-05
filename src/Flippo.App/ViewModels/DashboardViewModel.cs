using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Core.Domain;
using Flippo.Core.Session;
using Flippo.Core.Statistics;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>
/// Startseite (Port des Android-Dashboards): Begrüßung + Streak, Kennzahl-Kacheln (fällig/neu/Leeches),
/// „Jetzt lernen"-CTA, letzte Session und Schnellzugriffe. Kennzahlen kommen aus dem vorhandenen
/// <see cref="StatisticsCalculator"/> (P10).
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase, IActivatable
{
    private readonly VocabularyStore _store;
    private readonly SessionStore _sessions;
    private readonly NavigationService _nav;
    private readonly SetActionsService _actions;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasSets;

    [ObservableProperty] private string _greeting = "";
    [ObservableProperty] private int _streakDays;
    [ObservableProperty] private bool _hasStreak;
    [ObservableProperty] private string _streakText = "";
    [ObservableProperty] private string _dueCountText = "";

    [ObservableProperty] private int _dueToday;
    [ObservableProperty] private int _newCards;
    [ObservableProperty] private int _leechCards;

    /// <summary>Alle jetzt lernbaren Karten (fällig inkl. neu) — Zahl im CTA.</summary>
    [ObservableProperty] private int _dueTotal;
    [ObservableProperty] private bool _hasDue;

    [ObservableProperty] private bool _hasLastSession;
    [ObservableProperty] private string _lastSessionText = "";

    public DashboardViewModel(VocabularyStore store, SessionStore sessions, NavigationService nav, SetActionsService actions)
    {
        _store = store;
        _sessions = sessions;
        _nav = nav;
        _actions = actions;
    }

    public Task OnActivatedAsync() => LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var sets = await _store.GetSetsWithCountsAsync(now);
            var entries = await _store.GetAllEntriesAsync();
            var records = await _sessions.GetAllAsync();

            HasSets = sets.Count > 0;
            Greeting = L.T("Dash_Greeting");

            var stats = StatisticsCalculator.Compute(entries, records, now, TimeZoneInfo.Local);
            StreakDays = stats.StreakDays;
            HasStreak = stats.StreakDays >= 2;
            DueToday = stats.DueToday;
            NewCards = stats.NewCards;
            LeechCards = stats.LeechCards;
            DueTotal = stats.DueToday + stats.NewCards;   // fällige + neue = jetzt lernbar
            HasDue = DueTotal > 0;
            StreakText = string.Format(L.T("Dash_StreakFormat"), StreakDays);
            DueCountText = string.Format(L.T("Dash_DueCountFormat"), DueTotal);

            var last = records.Count > 0 ? records[0] : null;   // GetAllAsync sortiert StartedAt absteigend
            HasLastSession = last is not null;
            LastSessionText = last is null ? "" :
                string.Format(L.T("Dash_LastSessionFormat"), last.SetName, ModeName(last.LearningMode), last.CorrectCount, last.Total);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void LearnAllDue()
        => _nav.NavigateTo<LearnSessionViewModel>(
            vm => vm.Initialize(null, L.T("SetsVm_AllDueName"), SessionFilter.Due, LearningMode.Flashcard));

    [RelayCommand] private void GoSets() => _nav.NavigateTo<SetsOverviewViewModel>(clearStack: true);
    [RelayCommand] private void GoStatistics() => _nav.NavigateTo<StatisticsViewModel>(clearStack: true);

    [RelayCommand]
    private async Task NewSet()
    {
        if (await _actions.NewSetAsync()) GoSets();
    }

    private static string ModeName(string mode) => mode switch
    {
        "FLASHCARD" => L.T("Sets_ModeFlashcard"),
        "FREE_TEXT" => L.T("Sets_ModeFreeText"),
        "MULTIPLE_CHOICE" => L.T("Sets_ModeMultipleChoice"),
        _ => mode
    };
}
