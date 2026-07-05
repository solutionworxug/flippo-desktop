using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Core.Dictionary;
using Flippo.Core.Domain;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>
/// Wörterbuch-Detailansicht (Port von DictionaryDetailScreen): akzent-insensitive Suche über die
/// Einträge + „Als Karte in Kartei übernehmen" (Set-Picker → VocabularyEntry, mit Duplikat-Check).
/// </summary>
public sealed partial class UserDictionaryDetailViewModel : ViewModelBase
{
    private readonly UserDictionaryStore _store;
    private readonly VocabularyStore _vocab;
    private readonly NavigationService _nav;
    private readonly IDialogService _dialogs;

    private List<UserDictionaryEntry> _all = new();
    private long _dictId;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _resultCount = "";

    public ObservableCollection<UserDictionaryEntry> Results { get; } = new();

    public UserDictionaryDetailViewModel(UserDictionaryStore store, VocabularyStore vocab,
        NavigationService nav, IDialogService dialogs)
    {
        _store = store;
        _vocab = vocab;
        _nav = nav;
        _dialogs = dialogs;
    }

    public void Initialize(long dictId, string name)
    {
        _dictId = dictId;
        Title = name;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _all = (await _store.GetEntriesAsync(_dictId)).ToList();
        Apply();
    }

    partial void OnSearchTextChanged(string value) => Apply();

    private void Apply()
    {
        Results.Clear();
        foreach (var e in DictionarySearch.Filter(_all, SearchText)) Results.Add(e);
        IsEmpty = Results.Count == 0;
        ResultCount = string.Format(L.T("Dict_ResultCount"), Results.Count, _all.Count);
    }

    [RelayCommand]
    private async Task AddToSet(UserDictionaryEntry? e)
    {
        if (e is null) return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var sets = await _vocab.GetSetsWithCountsAsync(now);
        if (sets.Count == 0)
        {
            await _dialogs.ShowMessageAsync(L.T("Dict_AddToSet"), L.T("Dict_NoSets"));
            return;
        }

        var target = await _dialogs.ShowSetChooserAsync(sets);
        if (target is null) return;

        var existing = await _vocab.GetEntriesAsync(target.Id);
        if (existing.Any(x => string.Equals(x.SourceText, e.SourceWord, StringComparison.OrdinalIgnoreCase)))
        {
            await _dialogs.ShowMessageAsync(L.T("Dict_AddToSet"),
                string.Format(L.T("Dict_AlreadyInSet"), e.SourceWord, target.Title));
            return;
        }

        var (word, accepted) = SplitAlternatives(e.TargetWord);
        await _vocab.AddEntryAsync(new VocabularyEntry
        {
            SetId = target.Id,
            SourceText = e.SourceWord,
            TargetText = word,
            AcceptedAnswers = accepted,
            ExampleSentence = e.ExampleSentence,
            PartOfSpeech = e.PartOfSpeech,
            Gender = e.Gender,
            Difficulty = 250,
            BoxLevel = 1,
            NextReviewAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });

        await _dialogs.ShowMessageAsync(L.T("Dict_AddToSet"),
            string.Format(L.T("Dict_AddedToSet"), e.SourceWord, target.Title));
    }

    /// <summary>„laufen / rennen" → ("laufen", ["rennen"]) — gleiche Semantik wie ImportEngine.</summary>
    private static (string, IReadOnlyList<string>) SplitAlternatives(string target)
    {
        var parts = target.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 1) return (target.Trim(), []);
        return (parts[0], parts.Skip(1).ToList());
    }

    [RelayCommand] private void Back() => _nav.GoBack();
}
