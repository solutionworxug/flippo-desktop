using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Core.Backup;
using Flippo.Core.Domain;
using Flippo.Core.Import;
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
    private readonly FileImportService _fileImport;
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
        FileImportService fileImport,
        SettingsService settings)
    {
        _store = store;
        _nav = nav;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _backup = backup;
        _fileImport = fileImport;
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
        var stream = await _filePicker.OpenReadStreamAsync(L.T("SetsVm_ImportPickerTitle"));
        if (stream is null) return;

        BackupParseResult parsed;
        try
        {
            await using (stream)
                parsed = await _backup.ParseAsync(stream);
        }
        catch (BackupFormatException ex)
        {
            await _dialogs.ShowMessageAsync(L.T("SetsVm_ImportFailedTitle"), ex.Message);
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

        var message = string.Format(L.T("SetsVm_ImportSummary"), result.SetsImported, result.EntriesImported, result.SessionsImported);
        if (result.EntriesSkipped > 0)
            message += "\n" + string.Format(L.T("SetsVm_ImportSkipped"), result.EntriesSkipped);
        await _dialogs.ShowMessageAsync(L.T("SetsVm_ImportDoneTitle"), message);
    }

    /// <summary>CSV/TSV/TXT in eine (neue oder bestehende) Kartei importieren — Dialog mit Spalten-Mapping.</summary>
    [RelayCommand]
    private async Task ImportFile()
    {
        var picked = await _filePicker.OpenReadFileAsync(
            L.T("SetsVm_FileImportPickerTitle"), L.T("SetsVm_FileImportFilter"), "*.csv", "*.tsv", "*.txt", "*.xlsx");
        if (picked is null) return;

        IReadOnlyList<IReadOnlyList<string>> rows;
        await using (picked.Stream)
        {
            if (picked.Name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                // ClosedXML braucht einen seekbaren Stream → in den Speicher kopieren.
                using var mem = new MemoryStream();
                await picked.Stream.CopyToAsync(mem);
                mem.Position = 0;
                rows = XlsxReader.Read(mem);
            }
            else
            {
                using var reader = new StreamReader(picked.Stream);
                var content = await reader.ReadToEndAsync();
                rows = ImportEngine.ParseDelimited(content, DetectDelimiter(picked.Name, content));
            }
        }

        if (rows.Count == 0)
        {
            await _dialogs.ShowMessageAsync(L.T("SetsVm_FileImportDoneTitle"), L.T("SetsVm_FileImportEmpty"));
            return;
        }

        var existingSets = await _store.GetSetsWithCountsAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var request = await _dialogs.ShowFileImportAsync(picked.Name, rows, existingSets);
        if (request is null) return;   // abgebrochen

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long setId = request.ExistingSetId
            ?? await _store.AddSetAsync(new VocabularySet { Title = request.NewSetName, CreatedAt = now, UpdatedAt = now });

        var result = await _fileImport.ImportRowsAsync(rows, setId, request.Mapping, now, request.FirstRowIsHeader);
        await LoadAsync();

        var message = string.Format(L.T("SetsVm_FileImportSummary"), result.Imported, result.Duplicates, result.Skipped);
        await _dialogs.ShowMessageAsync(L.T("SetsVm_FileImportDoneTitle"), message);
    }

    /// <summary>.tsv → Tab; sonst per Heuristik der ersten Datenzeile (mehr Tabs als Kommas → Tab).</summary>
    private static char DetectDelimiter(string fileName, string content)
    {
        if (fileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase)) return '\t';
        var firstLine = content.ReplaceLineEndings("\n").Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
        return firstLine.Count(c => c == '\t') > firstLine.Count(c => c == ',') ? '\t' : ',';
    }

    [RelayCommand]
    private async Task Export()
    {
        var suggested = $"flippo-backup-{DateTimeOffset.Now:yyyy-MM-dd}.json";
        var stream = await _filePicker.SaveWriteStreamAsync(L.T("SetsVm_ExportPickerTitle"), suggested);
        if (stream is null) return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var srs = SettingsService.ToSrsSettings(_settings.Load());
        await using (stream)
            await _backup.ExportAsync(stream, srs, now);

        await _dialogs.ShowMessageAsync(L.T("SetsVm_ExportDoneTitle"), L.T("SetsVm_ExportDoneMsg"));
    }
}
