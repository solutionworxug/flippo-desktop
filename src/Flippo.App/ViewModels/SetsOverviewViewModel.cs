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
    private readonly LearnLauncher _launcher;

    public ObservableCollection<VocabularySet> Sets { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEmpty;

    public SetsOverviewViewModel(VocabularyStore store, NavigationService nav, SetActionsService actions, LearnLauncher launcher)
    {
        _store = store;
        _nav = nav;
        _actions = actions;
        _launcher = launcher;
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

    /// <summary>"Alle fälligen lernen" — Session über alle Karteien; Modus per Dialog.</summary>
    [RelayCommand]
    private Task LearnAllDue()
        => _launcher.StartAsync(null, L.T("SetsVm_AllDueName"), SessionFilter.Due);

    /// <summary>Kontextmenü: fällige Karten dieser Kartei lernen; Modus per Dialog.</summary>
    [RelayCommand]
    private Task LearnSet(VocabularySet? set)
        => set is null ? Task.CompletedTask : _launcher.StartAsync(set.Id, set.Title, SessionFilter.Due);

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
