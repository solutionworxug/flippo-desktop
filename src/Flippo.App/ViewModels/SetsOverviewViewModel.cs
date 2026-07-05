using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Services;
using Flippo.Core.Backup;
using Flippo.Core.Domain;
using Flippo.Core.Session;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>Übersicht aller Karteien mit Zählern gesamt/fällig/neu; Backup-Import/-Export.</summary>
public sealed partial class SetsOverviewViewModel : ViewModelBase, IActivatable
{
    private readonly VocabularyStore _store;
    private readonly NavigationService _nav;
    private readonly IFilePickerService _filePicker;
    private readonly IDialogService _dialogs;
    private readonly BackupService _backup;
    private readonly SettingsService _settings;

    public ObservableCollection<VocabularySet> Sets { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEmpty;

    public SetsOverviewViewModel(
        VocabularyStore store,
        NavigationService nav,
        IFilePickerService filePicker,
        IDialogService dialogs,
        BackupService backup,
        SettingsService settings)
    {
        _store = store;
        _nav = nav;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _backup = backup;
        _settings = settings;
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

    /// <summary>"Alle fälligen lernen" — Session über alle Karteien mit fälligen Karten.</summary>
    [RelayCommand]
    private void LearnAllDue()
        => _nav.NavigateTo<LearnSessionViewModel>(
            vm => vm.Initialize(null, "Alle fälligen", SessionFilter.Due, LearningMode.Flashcard));

    [RelayCommand]
    private async Task NewSet()
    {
        var set = await _dialogs.ShowSetEditorAsync(null);
        if (set is null) return;
        await _store.AddSetAsync(set);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Import()
    {
        var stream = await _filePicker.OpenReadStreamAsync("Backup importieren");
        if (stream is null) return;

        BackupParseResult parsed;
        try
        {
            await using (stream)
                parsed = await _backup.ParseAsync(stream);
        }
        catch (BackupFormatException ex)
        {
            await _dialogs.ShowMessageAsync("Import fehlgeschlagen", ex.Message);
            return;
        }

        var confirm = await _dialogs.ShowImportPreviewAsync(parsed);
        if (confirm is null) return;   // abgebrochen

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var result = await _backup.ImportAsync(parsed.Content, writeSafetyExport: true, now);

        if (confirm.ApplySettings && parsed.Content.Settings is not null)
        {
            var updated = SettingsService.WithSrs(_settings.Load(), parsed.Content.Settings);
            _settings.Save(updated);
        }

        await LoadAsync();

        var message = $"{result.SetsImported} Karteien, {result.EntriesImported} Karten, {result.SessionsImported} Sessions importiert.";
        if (result.EntriesSkipped > 0)
            message += $"\n{result.EntriesSkipped} Karte(n) mit unbekanntem Set übersprungen.";
        await _dialogs.ShowMessageAsync("Import abgeschlossen", message);
    }

    [RelayCommand]
    private async Task Export()
    {
        var suggested = $"flippo-backup-{DateTimeOffset.Now:yyyy-MM-dd}.json";
        var stream = await _filePicker.SaveWriteStreamAsync("Backup exportieren", suggested);
        if (stream is null) return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var srs = SettingsService.ToSrsSettings(_settings.Load());
        await using (stream)
            await _backup.ExportAsync(stream, srs, now);

        await _dialogs.ShowMessageAsync("Export abgeschlossen", "Das Backup wurde gespeichert.");
    }
}
