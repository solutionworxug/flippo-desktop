using Flippo.App.Localization;
using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Catalog;
using Flippo.Core.Backup;
using Flippo.Core.Content;
using Flippo.Core.Domain;
using Flippo.Core.Import;
using Flippo.Data.Services;

namespace Flippo.App.Services;

/// <summary>
/// Geteilte Kartei-Aktionen (Neue Kartei, Datei-Import, Backup-Import/-Export), genutzt sowohl von der
/// Sets-Übersicht (Toolbar) als auch vom Anwendungsmenü (P-Menü). Kapselt FilePicker + Dialoge + Services,
/// damit es keine Duplizierung gibt. Rückgabe <c>bool</c> = „Daten wurden geändert" → der Aufrufer lädt
/// die Ansicht danach neu.
/// </summary>
public sealed class SetActionsService
{
    private readonly VocabularyStore _store;
    private readonly IFilePickerService _filePicker;
    private readonly IDialogService _dialogs;
    private readonly BackupService _backup;
    private readonly FileImportService _fileImport;
    private readonly ThemeSetImporter _themeSets;
    private readonly IThemeSetSource _bundledSource;
    private readonly CatalogClient _catalog;
    private readonly InstalledPacksRegistry _installed;
    private readonly SettingsService _settings;
    private readonly CloudBackupService _cloud;
    private readonly DestinationStore _destinations;

    public SetActionsService(VocabularyStore store, IFilePickerService filePicker, IDialogService dialogs,
        BackupService backup, FileImportService fileImport, ThemeSetImporter themeSets, IThemeSetSource bundledSource,
        CatalogClient catalog, InstalledPacksRegistry installed, SettingsService settings,
        CloudBackupService cloud, DestinationStore destinations)
    {
        _store = store;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _backup = backup;
        _fileImport = fileImport;
        _themeSets = themeSets;
        _bundledSource = bundledSource;
        _catalog = catalog;
        _installed = installed;
        _settings = settings;
        _cloud = cloud;
        _destinations = destinations;
    }

    /// <summary>Themenset-Picker öffnen (Zielsprache aus UI-Sprache). True, wenn etwas importiert wurde.</summary>
    public Task<bool> ImportThemeSetAsync()
    {
        var uiLang = _settings.Load().UiLanguage;
        var target = uiLang.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "Englisch" : "Deutsch";
        return _dialogs.ShowThemeSetPickerAsync(_themeSets, _bundledSource, _catalog, _installed, target);
    }

    /// <summary>Set-Editor-Dialog öffnen und neue Kartei anlegen. True, wenn erstellt.</summary>
    public async Task<bool> NewSetAsync()
    {
        var set = await _dialogs.ShowSetEditorAsync(null);
        if (set is null) return false;
        await _store.AddSetAsync(set);
        return true;
    }

    /// <summary>Backup-JSON importieren (Full-Wipe wie Android, mit Preview + Safety-Export). True, wenn importiert.</summary>
    public async Task<bool> ImportBackupAsync()
    {
        var stream = await _filePicker.OpenReadStreamAsync(L.T("SetsVm_ImportPickerTitle"));
        if (stream is null) return false;

        BackupParseResult parsed;
        try
        {
            await using (stream)
                parsed = await _backup.ParseAsync(stream);
        }
        catch (BackupFormatException ex)
        {
            await _dialogs.ShowMessageAsync(L.T("SetsVm_ImportFailedTitle"), ex.Message);
            return false;
        }

        return await CompleteImportAsync(parsed);
    }

    /// <summary>Gemeinsamer Abschluss für Datei- und Ziel-Restore: Preview → Import → Settings → Summary.</summary>
    private async Task<bool> CompleteImportAsync(BackupParseResult parsed)
    {
        var confirm = await _dialogs.ShowImportPreviewAsync(parsed);
        if (confirm is null) return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var result = await _backup.ImportAsync(parsed.Content, writeSafetyExport: true, now);

        if (confirm.ApplySettings && parsed.Content.Settings is not null)
        {
            var updated = SettingsService.WithSrs(_settings.Load(), parsed.Content.Settings);
            _settings.Save(updated);
        }

        var message = string.Format(L.T("SetsVm_ImportSummary"), result.SetsImported, result.EntriesImported, result.SessionsImported);
        if (result.EntriesSkipped > 0)
            message += "\n" + string.Format(L.T("SetsVm_ImportSkipped"), result.EntriesSkipped);
        await _dialogs.ShowMessageAsync(L.T("SetsVm_ImportDoneTitle"), message);
        return true;
    }

    /// <summary>CSV/TSV/TXT/XLSX in eine (neue oder bestehende) Kartei importieren (Spalten-Mapping-Dialog). True, wenn importiert.</summary>
    public async Task<bool> ImportFileAsync()
    {
        var picked = await _filePicker.OpenReadFileAsync(
            L.T("SetsVm_FileImportPickerTitle"), L.T("SetsVm_FileImportFilter"), "*.csv", "*.tsv", "*.txt", "*.xlsx");
        if (picked is null) return false;

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
            return false;
        }

        var existingSets = await _store.GetSetsWithCountsAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var request = await _dialogs.ShowFileImportAsync(picked.Name, rows, existingSets);
        if (request is null) return false;   // abgebrochen

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long setId = request.ExistingSetId
            ?? await _store.AddSetAsync(new VocabularySet { Title = request.NewSetName, CreatedAt = now, UpdatedAt = now });

        var result = await _fileImport.ImportRowsAsync(rows, setId, request.Mapping, now, request.FirstRowIsHeader);
        var message = string.Format(L.T("SetsVm_FileImportSummary"), result.Imported, result.Duplicates, result.Skipped);
        await _dialogs.ShowMessageAsync(L.T("SetsVm_FileImportDoneTitle"), message);
        return true;
    }

    /// <summary>Backup-JSON exportieren (Datei-Speichern-Dialog).</summary>
    public async Task ExportBackupAsync()
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

    /// <summary>Sichert ein Backup zum gewählten Ziel (Task 7).</summary>
    public async Task<bool> ExportToDestinationAsync(DestinationConfig config)
    {
        try
        {
            var dest = _destinations.Resolve(config);
            var srs = SettingsService.ToSrsSettings(_settings.Load());
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var info = await _cloud.BackupToDestinationAsync(dest, srs, now);
            await _dialogs.ShowMessageAsync(L.T("Dest_BackupDoneTitle"),
                string.Format(L.T("Dest_BackupDoneMsg"), info.FileName, config.DisplayName));
            return true;
        }
        catch (DestinationException ex)
        {
            await _dialogs.ShowMessageAsync(L.T("Dest_ErrorTitle"), DestErrorMessage(ex));
            return false;
        }
    }

    /// <summary>Listet Backups des Ziels, lässt auswählen und stellt wieder her (Task 7).</summary>
    public async Task<bool> RestoreFromDestinationAsync(DestinationConfig config)
    {
        BackupParseResult parsed;
        try
        {
            var dest = _destinations.Resolve(config);
            var backups = await _cloud.ListBackupsAsync(dest);
            if (backups.Count == 0)
            {
                await _dialogs.ShowMessageAsync(L.T("Dest_RestoreTitle"), L.T("Dest_NoBackups"));
                return false;
            }

            var chosen = await _dialogs.ShowBackupChooserAsync(backups);
            if (chosen is null) return false;

            parsed = await _cloud.DownloadAndParseAsync(dest, chosen.RemoteId);
        }
        catch (DestinationException ex)
        {
            await _dialogs.ShowMessageAsync(L.T("Dest_ErrorTitle"), DestErrorMessage(ex));
            return false;
        }
        catch (BackupFormatException ex)
        {
            await _dialogs.ShowMessageAsync(L.T("SetsVm_ImportFailedTitle"), ex.Message);
            return false;
        }

        return await CompleteImportAsync(parsed);
    }

    private static string DestErrorMessage(DestinationException ex) => ex.State switch
    {
        DestinationState.NotConnected => L.T("Dest_ErrNotConnected"),
        DestinationState.Offline => L.T("Dest_ErrOffline"),
        DestinationState.QuotaExceeded => L.T("Dest_ErrQuota"),
        _ => L.T("Dest_ErrTransport")
    };

    /// <summary>.tsv → Tab; sonst per Heuristik der ersten Datenzeile (mehr Tabs als Kommas → Tab).</summary>
    private static char DetectDelimiter(string fileName, string content)
    {
        if (fileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase)) return '\t';
        var firstLine = content.ReplaceLineEndings("\n").Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
        return firstLine.Count(c => c == '\t') > firstLine.Count(c => c == ',') ? '\t' : ',';
    }
}
