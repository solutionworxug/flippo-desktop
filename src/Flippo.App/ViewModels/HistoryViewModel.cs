using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Core.Domain;
using Flippo.Core.Session;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>
/// Verlauf (Port von HistoryScreen): alle Lern-Sessions absteigend, je Zeile „Nur Falsche
/// wiederholen" (aus <see cref="SessionRecord.WrongEntryIds"/>) und „Set erneut lernen"
/// (falls das Set noch existiert). Erreichbar aus Statistik + Menü Ansicht.
/// </summary>
public sealed partial class HistoryViewModel : ViewModelBase, IActivatable
{
    private readonly SessionStore _sessions;
    private readonly VocabularyStore _store;
    private readonly NavigationService _nav;

    public ObservableCollection<HistoryRow> Rows { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEmpty;

    public HistoryViewModel(SessionStore sessions, VocabularyStore store, NavigationService nav)
    {
        _sessions = sessions;
        _store = store;
        _nav = nav;
    }

    public Task OnActivatedAsync() => LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var records = await _sessions.GetAllAsync();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var existingSetIds = (await _store.GetSetsWithCountsAsync(now)).Select(s => s.Id).ToHashSet();

            Rows.Clear();
            foreach (var r in records) Rows.Add(HistoryRow.From(r, existingSetIds));
            IsEmpty = Rows.Count == 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void RepeatWrong(HistoryRow? row)
    {
        if (row is null || !row.HasWrong) return;
        var ids = ParseIds(row.WrongEntryIds);
        if (ids.Count == 0) return;
        _nav.NavigateTo<LearnSessionViewModel>(
            vm => vm.InitializeFromIds(ids, string.Format(L.T("History_WrongSessionName"), row.SetName), row.Mode));
    }

    [RelayCommand]
    private void RelearnSet(HistoryRow? row)
    {
        if (row is null || !row.CanRelearn || row.SetId is not long setId) return;
        _nav.NavigateTo<LearnSessionViewModel>(
            vm => vm.Initialize(setId, row.SetName, SessionFilter.All, row.Mode));
    }

    [RelayCommand] private void Back() => _nav.GoBack();

    private static List<long> ParseIds(string csv)
        => csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var id) ? id : (long?)null)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToList();
}

/// <summary>Anzeige-Zeile für den Verlauf (formatiert, damit keine XAML-Converter nötig sind).</summary>
public sealed record HistoryRow(
    long Id,
    long? SetId,
    string SetName,
    LearningMode Mode,
    string ModeDisplay,
    string DateText,
    string QuoteText,
    string DurationText,
    string WrongEntryIds,
    bool HasWrong,
    bool CanRelearn)
{
    public static HistoryRow From(SessionRecord r, IReadOnlySet<long> existingSetIds)
    {
        var mode = ParseMode(r.LearningMode);
        var date = DateTimeOffset.FromUnixTimeMilliseconds(r.StartedAt).LocalDateTime.ToString("g");
        var quote = string.Format(L.T("History_QuoteFormat"), r.CorrectCount, r.Total,
            Math.Round(r.SuccessRate * 100));
        var duration = string.Format(L.T("History_DurationFormat"), r.DurationMinutes);
        bool canRelearn = r.SetId is long id && existingSetIds.Contains(id);
        return new HistoryRow(r.Id, r.SetId, r.SetName, mode, ModeName(r.LearningMode),
            date, quote, duration, r.WrongEntryIds, r.WrongCount > 0, canRelearn);
    }

    private static LearningMode ParseMode(string mode) => mode switch
    {
        "FLASHCARD" => LearningMode.Flashcard,
        "MULTIPLE_CHOICE" => LearningMode.MultipleChoice,
        _ => LearningMode.FreeText
    };

    private static string ModeName(string mode) => mode switch
    {
        "FLASHCARD" => L.T("Sets_ModeFlashcard"),
        "FREE_TEXT" => L.T("Sets_ModeFreeText"),
        "MULTIPLE_CHOICE" => L.T("Sets_ModeMultipleChoice"),
        _ => mode
    };
}
