using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Services;
using Flippo.Core.Domain;
using Flippo.Core.Session;
using Flippo.Core.Srs;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>
/// Führt eine Lern-Session aus (P6, Vertical Slice: Flashcard-Modus). Orchestriert nur —
/// die Zusammenstellung (<see cref="SessionComposer"/>), Richtung (<see cref="SessionDirections"/>)
/// und Bewertung (<see cref="SrsEngine"/>) liegen als reine, getestete Logik im Core.
/// Box-Modus zeigt 2 Bewertungsknöpfe (Falsch/Richtig), Adaptiv 4 mit Intervall-Vorschau (Dry-Run).
/// </summary>
public sealed partial class LearnSessionViewModel : ViewModelBase, IActivatable
{
    private readonly VocabularyStore _store;
    private readonly SessionStore _sessions;
    private readonly SettingsService _settingsService;
    private readonly NavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly Random _rng = new();

    // Session-Konfiguration
    private long? _setId;
    private string _setName = "";
    private SessionFilter _filter = SessionFilter.Due;
    private int _boxLevel;
    private LearningMode _mode = LearningMode.Flashcard;
    private IReadOnlyList<long>? _repeatIds;   // gesetzt = "Falsche wiederholen" statt Filter-Einstieg

    // Session-Zustand
    private SrsSettings _settings = new();
    private IReadOnlyList<VocabularyEntry> _cards = [];
    private IReadOnlyList<bool> _directions = [];
    private int _index;
    private long _startedAt;
    private readonly List<long> _wrongEntryIds = [];
    private UndoSnapshot? _undo;

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private string _questionText = "";
    [ObservableProperty] private string _answerText = "";
    [ObservableProperty] private bool _isAnswerShown;
    [ObservableProperty] private bool _isAdaptive;
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private int _correctCount;
    [ObservableProperty] private int _wrongCount;
    // Adaptiv-Intervall-Vorschau (Tage) für die 4 Knöpfe — SrsEngine-Dry-Run je Karte.
    [ObservableProperty] private string _previewAgain = "";
    [ObservableProperty] private string _previewHard = "";
    [ObservableProperty] private string _previewGood = "";
    [ObservableProperty] private string _previewEasy = "";

    public LearnSessionViewModel(
        VocabularyStore store, SessionStore sessions, SettingsService settingsService,
        NavigationService nav, IDialogService dialogs)
    {
        _store = store;
        _sessions = sessions;
        _settingsService = settingsService;
        _nav = nav;
        _dialogs = dialogs;
    }

    /// <summary>Einstieg über Split-Button (Set oder "alle") mit Filter (Fällige/Alle/Neue/Leeches).</summary>
    public void Initialize(long? setId, string setName, SessionFilter filter, LearningMode mode = LearningMode.Flashcard, int boxLevel = 0)
    {
        _setId = setId;
        _setName = setName;
        _filter = filter;
        _mode = mode;
        _boxLevel = boxLevel;
        _repeatIds = null;
        Title = setName;
    }

    /// <summary>Einstieg "Falsche wiederholen" — neue Session nur mit den angegebenen Karten (aktueller Zustand).</summary>
    public void InitializeFromIds(IReadOnlyList<long> ids, string setName, LearningMode mode)
    {
        _setId = null;
        _setName = setName;
        _mode = mode;
        _repeatIds = ids;
        Title = setName;
    }

    public async Task OnActivatedAsync()
    {
        var now = Now();
        _settings = SettingsService.ToSrsSettings(_settingsService.Load());
        IsAdaptive = _settings.Mode == SrsMode.Adaptive;

        IReadOnlyList<VocabularyEntry> candidates;
        SessionFilter filter;
        if (_repeatIds is not null)
        {
            candidates = await _store.GetEntriesByIdsAsync(_repeatIds);
            filter = SessionFilter.All;   // alle wiederholen, keine Fälligkeits-Filterung
        }
        else
        {
            candidates = _setId is long id ? await _store.GetEntriesAsync(id) : await _store.GetAllEntriesAsync();
            filter = _filter;
        }

        var plan = SessionComposer.Compose(
            candidates, new SessionComposeOptions { Filter = filter, BoxLevel = _boxLevel },
            _settings, _rng, now);

        _cards = plan.Cards;
        _directions = SessionDirections.Build(_cards.Count, _settings.LearningDirection, _mode, _rng);
        _startedAt = now;
        _index = 0;
        _wrongEntryIds.Clear();
        CorrectCount = 0;
        WrongCount = 0;
        CanUndo = false;
        _undo = null;

        if (_cards.Count == 0)
        {
            IsEmpty = true;
            return;
        }

        IsEmpty = false;
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        var card = _cards[_index];
        var sourceToTarget = _directions[_index];
        QuestionText = sourceToTarget ? card.SourceText : card.TargetText;
        AnswerText = sourceToTarget ? card.TargetText : card.SourceText;
        IsAnswerShown = false;
        ProgressText = $"{_index + 1} / {_cards.Count}";
        if (IsAdaptive) UpdatePreviews(card);
    }

    private void UpdatePreviews(VocabularyEntry card)
    {
        var now = Now();
        string Days(ReviewResult r) => $"{SrsEngine.Schedule(card, r, _settings, now).LastIntervalDays} T";
        PreviewAgain = Days(ReviewResult.Wrong);
        PreviewHard = Days(ReviewResult.Hard);
        PreviewGood = Days(ReviewResult.Good);
        PreviewEasy = Days(ReviewResult.Easy);
    }

    [RelayCommand] private void Flip() => IsAnswerShown = true;

    // Tastensteuerung: Box → 1/2 (Falsch/Richtig), Adaptiv → 1–4 (Nochmal/Schwer/Gut/Einfach).
    [RelayCommand]
    private Task GradeKey(string key)
    {
        if (!IsAnswerShown) return Task.CompletedTask;
        ReviewResult? result = IsAdaptive
            ? key switch { "1" => ReviewResult.Wrong, "2" => ReviewResult.Hard, "3" => ReviewResult.Good, "4" => ReviewResult.Easy, _ => null }
            : key switch { "1" => ReviewResult.Wrong, "2" => ReviewResult.Good, _ => null };
        return result is null ? Task.CompletedTask : Grade(result.Value);
    }

    // Direkte Button-Kommandos (identisch zu den Tasten, für Klick).
    [RelayCommand] private Task GradeWrong() => IsAnswerShown ? Grade(ReviewResult.Wrong) : Task.CompletedTask;
    [RelayCommand] private Task GradeHard() => IsAnswerShown ? Grade(ReviewResult.Hard) : Task.CompletedTask;
    [RelayCommand] private Task GradeGood() => IsAnswerShown ? Grade(ReviewResult.Good) : Task.CompletedTask;
    [RelayCommand] private Task GradeEasy() => IsAnswerShown ? Grade(ReviewResult.Easy) : Task.CompletedTask;

    private async Task Grade(ReviewResult result)
    {
        if (_index >= _cards.Count) return;
        var card = _cards[_index];
        var now = Now();

        // Snapshot des Karten-Zustands VOR dem Review — ermöglicht exaktes Undo (wie Android).
        _undo = new UndoSnapshot(card, _index, CorrectCount, WrongCount, _wrongEntryIds.Count);

        var update = SrsEngine.Schedule(card, result, _settings, now);
        await _store.ApplyReviewAsync(update);

        if (result.IsCorrect())
            CorrectCount++;
        else
        {
            WrongCount++;
            _wrongEntryIds.Add(card.Id);
        }

        CanUndo = true;
        _index++;

        if (_index >= _cards.Count)
            await FinishAsync();
        else
            ShowCurrent();
    }

    [RelayCommand]
    private async Task Undo()
    {
        if (_undo is null) return;
        var snap = _undo;

        // Kompletten Karten-Zustand in der DB zurücksetzen.
        await _store.ApplyReviewAsync(ToUpdate(snap.Card));

        _index = snap.Index;
        CorrectCount = snap.CorrectCount;
        WrongCount = snap.WrongCount;
        if (_wrongEntryIds.Count > snap.WrongIdCount)
            _wrongEntryIds.RemoveRange(snap.WrongIdCount, _wrongEntryIds.Count - snap.WrongIdCount);

        _undo = null;
        CanUndo = false;
        ShowCurrent();
        IsAnswerShown = true;   // Karte war beim Bewerten bereits aufgedeckt
    }

    [RelayCommand]
    private async Task Exit()
    {
        var confirmed = await _dialogs.ConfirmAsync("Session beenden", "Lern-Session wirklich beenden?", "Beenden");
        if (!confirmed) return;
        await FinishAsync();
    }

    [RelayCommand] private void BackToSets() => _nav.NavigateTo<SetsOverviewViewModel>(clearStack: true);

    private async Task FinishAsync()
    {
        var now = Now();
        // Desktop-Zusatz (Plan 1.4): auch bei Abbruch schreiben, wenn ≥1 Karte beantwortet.
        var record = SessionResult.Build(_setId, _setName, _mode, CorrectCount, WrongCount, _wrongEntryIds, _startedAt, now);
        if (record is not null)
            await _sessions.AddAsync(record);

        var wrongIds = _wrongEntryIds.ToList();
        var correct = CorrectCount;
        var wrong = WrongCount;
        _nav.NavigateTo<SessionSummaryViewModel>(
            vm => vm.Initialize(_setName, _mode, correct, wrong, wrongIds), clearStack: true);
    }

    private static VocabularyEntryUpdate ToUpdate(VocabularyEntry e) => new()
    {
        Id = e.Id,
        BoxLevel = e.BoxLevel,
        NextReviewAt = e.NextReviewAt,
        CorrectCount = e.CorrectCount,
        WrongCount = e.WrongCount,
        LastReviewedAt = e.LastReviewedAt,
        IsLeech = e.IsLeech,
        Difficulty = e.Difficulty,
        LastIntervalDays = e.LastIntervalDays,
        UpdatedAt = e.UpdatedAt
    };

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private sealed record UndoSnapshot(VocabularyEntry Card, int Index, int CorrectCount, int WrongCount, int WrongIdCount);
}
