using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Core.Checking;
using Flippo.Core.Domain;
using Flippo.Core.Session;
using Flippo.Core.Srs;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>
/// Führt eine Lern-Session aus (P6). Drei Modi teilen sich Ablauf, Undo und Abschluss;
/// nur Kartendarstellung und Antwort-Ermittlung unterscheiden sich:
/// Flashcard (umdrehen, Box 1/2 · Adaptiv 1–4), Freitext (tippen, Enter prüft),
/// Multiple Choice (1–4 wählen). Zusammenstellung/Richtung/Prüfung/Bewertung sind
/// reine, getestete Core-Logik (<see cref="SessionComposer"/>, <see cref="FreeTextChecker"/>, <see cref="SrsEngine"/>).
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
    private IReadOnlyList<long>? _repeatIds;

    // Session-Zustand
    private SrsSettings _settings = new();
    private IReadOnlyList<VocabularyEntry> _cards = [];
    private IReadOnlyList<VocabularyEntry> _allEntries = [];   // Fallback-Pool für MC-Distraktoren
    private IReadOnlyList<bool> _directions = [];
    private int _index;
    private long _startedAt;
    private readonly List<long> _wrongEntryIds = [];
    private UndoSnapshot? _undo;
    private ReviewResult? _pendingResult;   // Freitext/MC: ermitteltes Ergebnis, angewendet beim Weiterblättern

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private string _questionText = "";
    [ObservableProperty] private string _answerText = "";
    [ObservableProperty] private bool _isAnswerShown;
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private int _correctCount;
    [ObservableProperty] private int _wrongCount;

    // Modus-Flags für die View (welches Präsentations-Panel + welche Bewertungsknöpfe)
    [ObservableProperty] private bool _isFlashcard;
    [ObservableProperty] private bool _isFreeText;
    [ObservableProperty] private bool _isMultipleChoice;
    [ObservableProperty] private bool _isAdaptive;

    // Adaptiv-Intervall-Vorschau (Flashcard)
    [ObservableProperty] private string _previewAgain = "";
    [ObservableProperty] private string _previewHard = "";
    [ObservableProperty] private string _previewGood = "";
    [ObservableProperty] private string _previewEasy = "";

    // Freitext
    [ObservableProperty] private string _userInput = "";
    [ObservableProperty] private string _feedbackText = "";
    [ObservableProperty] private bool _feedbackIsPositive;

    // Multiple Choice
    public ObservableCollection<McOption> Options { get; } = [];

    /// <summary>Bittet die View, den Fokus in das Freitext-Feld zu setzen.</summary>
    public event Action? FocusInputRequested;

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

    /// <summary>Einstieg über Split-Button (Set oder "alle") mit Filter und Modus.</summary>
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

    /// <summary>Einstieg "Falsche wiederholen" — neue Session nur mit den angegebenen Karten.</summary>
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
        IsFlashcard = _mode == LearningMode.Flashcard;
        IsFreeText = _mode == LearningMode.FreeText;
        IsMultipleChoice = _mode == LearningMode.MultipleChoice;
        IsAdaptive = _settings.Mode == SrsMode.Adaptive;

        IReadOnlyList<VocabularyEntry> candidates;
        SessionFilter filter;
        if (_repeatIds is not null)
        {
            candidates = await _store.GetEntriesByIdsAsync(_repeatIds);
            filter = SessionFilter.All;
        }
        else
        {
            candidates = _setId is long id ? await _store.GetEntriesAsync(id) : await _store.GetAllEntriesAsync();
            filter = _filter;
        }

        // Für Multiple Choice die Gesamt-DB als Distraktor-Fallback (bei kleinen Sessions).
        _allEntries = _mode == LearningMode.MultipleChoice ? await _store.GetAllEntriesAsync() : candidates;

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
        UserInput = "";
        FeedbackText = "";
        _pendingResult = null;
        ProgressText = $"{_index + 1} / {_cards.Count}";

        if (IsFlashcard && IsAdaptive) UpdatePreviews(card);
        if (IsMultipleChoice) BuildOptions(card, sourceToTarget);
        if (IsFreeText) FocusInputRequested?.Invoke();
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

    private void BuildOptions(VocabularyEntry card, bool sourceToTarget)
    {
        var correct = sourceToTarget ? card.TargetText : card.SourceText;
        var texts = MultipleChoice.BuildOptions(card, sourceToTarget, _cards, _allEntries, _rng);
        Options.Clear();
        var n = 1;
        foreach (var t in texts)
            Options.Add(new McOption(n++, t, t == correct));
    }

    // ---- Flashcard ----

    [RelayCommand] private void Flip() => IsAnswerShown = true;

    [RelayCommand] private Task GradeWrong() => IsFlashcard && IsAnswerShown ? Grade(ReviewResult.Wrong) : Task.CompletedTask;
    [RelayCommand] private Task GradeHard() => IsFlashcard && IsAnswerShown ? Grade(ReviewResult.Hard) : Task.CompletedTask;
    [RelayCommand] private Task GradeGood() => IsFlashcard && IsAnswerShown ? Grade(ReviewResult.Good) : Task.CompletedTask;
    [RelayCommand] private Task GradeEasy() => IsFlashcard && IsAnswerShown ? Grade(ReviewResult.Easy) : Task.CompletedTask;

    // ---- Zahlentasten 1–4: modusabhängig (Flashcard = bewerten, MC = Option wählen) ----

    [RelayCommand]
    private Task NumberKey(string key)
    {
        if (IsMultipleChoice)
        {
            if (int.TryParse(key, out var n)) ChooseOptionByIndex(n - 1);
            return Task.CompletedTask;
        }
        if (IsFlashcard && IsAnswerShown)
        {
            ReviewResult? result = IsAdaptive
                ? key switch { "1" => ReviewResult.Wrong, "2" => ReviewResult.Hard, "3" => ReviewResult.Good, "4" => ReviewResult.Easy, _ => null }
                : key switch { "1" => ReviewResult.Wrong, "2" => ReviewResult.Good, _ => null };
            if (result is not null) return Grade(result.Value);
        }
        return Task.CompletedTask;
    }

    // ---- Freitext ----

    [RelayCommand]
    private Task Submit()   // Enter: erst prüfen, dann weiterblättern
    {
        if (IsFreeText)
        {
            if (!IsAnswerShown) { CheckFreeText(); return Task.CompletedTask; }
            return ApplyPendingAndAdvance();
        }
        if (IsMultipleChoice && IsAnswerShown)
            return ApplyPendingAndAdvance();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void DontKnow()   // F1: als falsch werten
    {
        if (!IsFreeText || IsAnswerShown) return;
        var card = _cards[_index];
        _pendingResult = ReviewResult.Wrong;
        FeedbackText = string.Format(L.T("Learn_FeedbackDontKnow"), AnswerText);
        FeedbackIsPositive = false;
        IsAnswerShown = true;
        _ = card;
    }

    private void CheckFreeText()
    {
        var card = _cards[_index];
        var outcome = FreeTextChecker.Check(
            UserInput, card, _settings.StrictAccents, _settings.TypoToleranceEnabled, BuildSiblings(card));

        // Plan 1.2: alles außer WRONG zählt als richtig (GOOD); kein HARD/EASY im Freitext.
        _pendingResult = outcome.Result == FreeTextChecker.CheckResult.Wrong ? ReviewResult.Wrong : ReviewResult.Good;
        FeedbackIsPositive = _pendingResult == ReviewResult.Good;
        FeedbackText = outcome.Result switch
        {
            FreeTextChecker.CheckResult.Correct => L.T("Learn_FeedbackCorrect"),
            FreeTextChecker.CheckResult.AlmostCorrect => string.Format(L.T("Learn_FeedbackAccents"), outcome.CorrectAnswer),
            FreeTextChecker.CheckResult.Typo => string.Format(L.T("Learn_FeedbackTypo"), outcome.CorrectAnswer),
            _ => string.Format(L.T("Learn_FeedbackWrong"), outcome.CorrectAnswer)
        };
        IsAnswerShown = true;
    }

    private List<string> BuildSiblings(VocabularyEntry card)
        => _cards
            .Where(c => c.Id != card.Id)
            .SelectMany(c => new[] { c.TargetText, c.SourceText }.Concat(c.AcceptedAnswers))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList();

    // ---- Multiple Choice ----

    [RelayCommand]
    private void ChooseOption(McOption? option)
    {
        if (option is null || IsAnswerShown) return;
        var index = Options.IndexOf(option);
        if (index >= 0) ChooseOptionByIndex(index);
    }

    private void ChooseOptionByIndex(int index)
    {
        if (IsAnswerShown || index < 0 || index >= Options.Count) return;
        var chosen = Options[index];
        chosen.IsChosen = true;
        foreach (var o in Options) o.Revealed = true;

        _pendingResult = chosen.IsCorrect ? ReviewResult.Good : ReviewResult.Wrong;
        FeedbackIsPositive = chosen.IsCorrect;
        FeedbackText = chosen.IsCorrect ? L.T("Learn_FeedbackCorrect") : string.Format(L.T("Learn_FeedbackWrong"), AnswerText);
        IsAnswerShown = true;
    }

    // ---- Gemeinsamer Ablauf ----

    private Task ApplyPendingAndAdvance()
        => _pendingResult is { } result ? Grade(result) : Task.CompletedTask;

    private async Task Grade(ReviewResult result)
    {
        if (_index >= _cards.Count) return;
        var card = _cards[_index];
        var now = Now();

        // Snapshot VOR dem Review — exaktes Undo (kompletter Karten-Zustand, wie Android).
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

        await _store.ApplyReviewAsync(ToUpdate(snap.Card));

        _index = snap.Index;
        CorrectCount = snap.CorrectCount;
        WrongCount = snap.WrongCount;
        if (_wrongEntryIds.Count > snap.WrongIdCount)
            _wrongEntryIds.RemoveRange(snap.WrongIdCount, _wrongEntryIds.Count - snap.WrongIdCount);

        _undo = null;
        CanUndo = false;
        ShowCurrent();
        if (IsFlashcard) IsAnswerShown = true;   // Antwort war beim Bewerten sichtbar; Freitext/MC neu beantworten
    }

    [RelayCommand]
    private async Task Exit()
    {
        var confirmed = await _dialogs.ConfirmAsync(L.T("Learn_ExitTitle"), L.T("Learn_ExitMessage"), L.T("Learn_ExitConfirm"));
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

/// <summary>Eine Multiple-Choice-Option mit Reveal-Zustand für das Farb-Feedback nach der Wahl.</summary>
public sealed partial class McOption(int number, string text, bool isCorrect) : ObservableObject
{
    public int Number { get; } = number;
    public string Text { get; } = text;
    public bool IsCorrect { get; } = isCorrect;
    public string Label => $"{Number}.  {Text}";

    [ObservableProperty] private bool _revealed;   // nach der Auswahl: Optionen aufgedeckt
    [ObservableProperty] private bool _isChosen;   // vom Nutzer gewählte Option
}
