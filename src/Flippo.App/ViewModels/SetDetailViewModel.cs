using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Services;
using Flippo.Core.Domain;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>Detailansicht einer Kartei mit DataGrid, Suche und tastatur-first CardEditor (P5).</summary>
public sealed partial class SetDetailViewModel : ViewModelBase, IActivatable
{
    private readonly VocabularyStore _store;
    private readonly NavigationService _nav;
    private readonly IDialogService _dialogs;

    private List<VocabularyEntry> _entities = new();
    private VocabularySet _set = new();

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private EntryRow? _selectedRow;
    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty] private CardEditorViewModel? _editor;

    public ObservableCollection<EntryRow> Entries { get; } = new();

    /// <summary>Bittet die View, den Fokus zurück auf das Quelle-Feld zu setzen (Schnellanlage-Loop).</summary>
    public event Action? FocusSourceRequested;

    public SetDetailViewModel(VocabularyStore store, NavigationService nav, IDialogService dialogs)
    {
        _store = store;
        _nav = nav;
        _dialogs = dialogs;
    }

    public void Initialize(VocabularySet set)
    {
        _set = set;
        Title = set.Title;
    }

    public Task OnActivatedAsync() => LoadEntriesAsync();

    private async Task LoadEntriesAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _entities = (await _store.GetEntriesAsync(_set.Id)).ToList();
        ApplyFilter(now);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    private void ApplyFilter(long nowMs)
    {
        var query = SearchText.Trim();
        IEnumerable<VocabularyEntry> filtered = _entities;
        if (query.Length > 0)
        {
            filtered = _entities.Where(e =>
                e.SourceText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.TargetText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        Entries.Clear();
        foreach (var e in filtered) Entries.Add(EntryRow.From(e, nowMs));
    }

    // ---- CardEditor ----

    [RelayCommand]
    private void NewCard()
    {
        Editor = new CardEditorViewModel(NewEntryTemplate());
        IsEditorOpen = true;
        FocusSourceRequested?.Invoke();
    }

    [RelayCommand]
    private void EditSelected()
    {
        if (SelectedRow is null) return;
        var entity = _entities.FirstOrDefault(e => e.Id == SelectedRow.Id);
        if (entity is null) return;
        Editor = new CardEditorViewModel(entity);
        IsEditorOpen = true;
        FocusSourceRequested?.Invoke();
    }

    [RelayCommand]
    private async Task SaveAndNext()
    {
        if (!await PersistAsync()) return;
        await LoadEntriesAsync();
        Editor = new CardEditorViewModel(NewEntryTemplate());   // Schnellanlage-Loop
        IsEditorOpen = true;
        FocusSourceRequested?.Invoke();
    }

    [RelayCommand]
    private async Task SaveAndClose()
    {
        if (!await PersistAsync()) return;
        CloseEditor();
        await LoadEntriesAsync();
    }

    [RelayCommand]
    private void CancelEdit() => CloseEditor();

    [RelayCommand]
    private async Task DeleteSelected()
    {
        if (SelectedRow is null) return;
        var confirmed = await _dialogs.ConfirmAsync(
            "Karte löschen",
            $"„{SelectedRow.SourceText}“ wirklich löschen?",
            "Löschen");
        if (!confirmed) return;

        await _store.DeleteEntryAsync(SelectedRow.Id);
        await LoadEntriesAsync();
    }

    private async Task<bool> PersistAsync()
    {
        if (Editor is null || !Editor.HasContent) return false;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var entry = Editor.Build(now);
        if (entry.Id == 0)
            await _store.AddEntryAsync(entry);
        else
            await _store.UpdateEntryAsync(entry);
        return true;
    }

    private void CloseEditor()
    {
        Editor = null;
        IsEditorOpen = false;
    }

    private VocabularyEntry NewEntryTemplate()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new VocabularyEntry
        {
            SetId = _set.Id,
            Difficulty = 250,          // Entity-Default
            BoxLevel = 1,
            NextReviewAt = now,        // sofort fällig/neu
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    // ---- Set-CRUD ----

    [RelayCommand]
    private async Task EditSet()
    {
        var updated = await _dialogs.ShowSetEditorAsync(_set);
        if (updated is null) return;
        await _store.UpdateSetAsync(updated);
        _set = updated;
        Title = updated.Title;
    }

    [RelayCommand]
    private async Task DeleteSet()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "Kartei löschen",
            $"„{_set.Title}“ mit allen Karten unwiderruflich löschen?",
            "Löschen");
        if (!confirmed) return;

        await _store.DeleteSetAsync(_set.Id);
        _nav.GoBack();
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
