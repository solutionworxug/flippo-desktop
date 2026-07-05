using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Core.Domain;
using Flippo.Core.Session;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>Übersicht aller Karteien mit Zählern gesamt/fällig/neu. Aktionen laufen über <see cref="SetActionsService"/>.</summary>
public sealed partial class SetsOverviewViewModel : ViewModelBase, IActivatable
{
    private readonly VocabularyStore _store;
    private readonly NavigationService _nav;
    private readonly SetActionsService _actions;

    public ObservableCollection<VocabularySet> Sets { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEmpty;

    public SetsOverviewViewModel(VocabularyStore store, NavigationService nav, SetActionsService actions)
    {
        _store = store;
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
            Sets.Clear();
            foreach (var s in sets) Sets.Add(s);
            IsEmpty = Sets.Count == 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenSet(VocabularySet? set)
    {
        if (set is null) return;
        _nav.NavigateTo<SetDetailViewModel>(vm => vm.Initialize(set));
    }

    /// <summary>"Alle fälligen lernen" — Session über alle Karteien; Parameter = Modus (Standard Karteikarten).</summary>
    [RelayCommand]
    private void LearnAllDue(string? mode)
        => _nav.NavigateTo<LearnSessionViewModel>(
            vm => vm.Initialize(null, L.T("SetsVm_AllDueName"), SessionFilter.Due, ParseMode(mode)));

    private static LearningMode ParseMode(string? s) => s switch
    {
        "FreeText" => LearningMode.FreeText,
        "MultipleChoice" => LearningMode.MultipleChoice,
        _ => LearningMode.Flashcard
    };

    /// <summary>Kontextmenü: fällige Karten dieser Kartei als Karteikarten-Session lernen.</summary>
    [RelayCommand]
    private void LearnSet(VocabularySet? set)
    {
        if (set is null) return;
        _nav.NavigateTo<LearnSessionViewModel>(
            vm => vm.Initialize(set.Id, set.Title, SessionFilter.Due, LearningMode.Flashcard));
    }

    [RelayCommand]
    private async Task NewSet()
    {
        if (await _actions.NewSetAsync()) await LoadAsync();
    }

    [RelayCommand]
    private async Task Import()
    {
        if (await _actions.ImportBackupAsync()) await LoadAsync();
    }

    [RelayCommand]
    private async Task ImportFile()
    {
        if (await _actions.ImportFileAsync()) await LoadAsync();
    }

    [RelayCommand]
    private async Task ImportThemeSet()
    {
        if (await _actions.ImportThemeSetAsync()) await LoadAsync();
    }

    [RelayCommand]
    private Task Export() => _actions.ExportBackupAsync();
}
