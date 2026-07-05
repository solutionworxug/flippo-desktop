using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Services;
using Flippo.Core.Domain;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>Read-only-Detailansicht einer Kartei (DataGrid) mit Suche/Filter. CRUD kommt in P5.</summary>
public sealed partial class SetDetailViewModel : ViewModelBase, IActivatable
{
    private readonly VocabularyStore _store;
    private readonly NavigationService _nav;
    private List<EntryRow> _all = new();
    private long _setId;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _searchText = "";

    public ObservableCollection<EntryRow> Entries { get; } = new();

    public SetDetailViewModel(VocabularyStore store, NavigationService nav)
    {
        _store = store;
        _nav = nav;
    }

    public void Initialize(VocabularySet set)
    {
        _setId = set.Id;
        Title = set.Title;
    }

    public Task OnActivatedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var entries = await _store.GetEntriesAsync(_setId);
        _all = entries.Select(e => EntryRow.From(e, now)).ToList();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        IEnumerable<EntryRow> filtered = _all;
        if (query.Length > 0)
        {
            filtered = _all.Where(r =>
                r.SourceText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.TargetText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.TagsDisplay.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        Entries.Clear();
        foreach (var row in filtered) Entries.Add(row);
    }

    [RelayCommand] private void Back() => _nav.GoBack();
}

/// <summary>Anzeige-Zeile für das DataGrid (formatierte Werte, damit keine XAML-Converter nötig sind).</summary>
public sealed record EntryRow(
    long Id,
    string SourceText,
    string TargetText,
    int BoxLevel,
    string DueDisplay,
    bool IsLeech,
    string TagsDisplay)
{
    public static EntryRow From(VocabularyEntry e, long nowMs)
    {
        var dueDisplay = e.NextReviewAt <= 0
            ? "—"
            : DateTimeOffset.FromUnixTimeMilliseconds(e.NextReviewAt).LocalDateTime.ToString("yyyy-MM-dd");
        return new EntryRow(e.Id, e.SourceText, e.TargetText, e.BoxLevel, dueDisplay, e.IsLeech, string.Join(", ", e.Tags));
    }
}
