using Avalonia.Controls;
using Flippo.App.ViewModels;
using Flippo.App.Views;
using Flippo.Core.Backup;
using Flippo.Core.Domain;

namespace Flippo.App.Services;

/// <summary>Ergebnis des Import-Preview-Dialogs (null = abgebrochen).</summary>
public sealed record ImportConfirmation(bool ApplySettings);

public interface IDialogService
{
    Task<ImportConfirmation?> ShowImportPreviewAsync(BackupParseResult parsed);

    /// <summary>Datei-Import-Dialog (P9): Spalten-Mapping + Ziel-Set. Rückgabe null = abgebrochen.</summary>
    Task<FileImportRequest?> ShowFileImportAsync(
        string fileName, IReadOnlyList<IReadOnlyList<string>> rows, IReadOnlyList<VocabularySet> existingSets);

    Task ShowMessageAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel = "OK");
    Task<VocabularySet?> ShowSetEditorAsync(VocabularySet? existing);
}

public sealed class DialogService : IDialogService
{
    private readonly Func<Window?> _owner;

    public DialogService(Func<Window?> owner) => _owner = owner;

    public async Task<ImportConfirmation?> ShowImportPreviewAsync(BackupParseResult parsed)
    {
        var owner = _owner();
        if (owner is null) return null;

        var window = new ImportPreviewWindow { DataContext = new ImportPreviewViewModel(parsed) };
        return await window.ShowDialog<ImportConfirmation?>(owner);
    }

    public async Task<FileImportRequest?> ShowFileImportAsync(
        string fileName, IReadOnlyList<IReadOnlyList<string>> rows, IReadOnlyList<VocabularySet> existingSets)
    {
        var owner = _owner();
        if (owner is null) return null;

        var window = new FileImportWindow { DataContext = new FileImportViewModel(fileName, rows, existingSets) };
        return await window.ShowDialog<FileImportRequest?>(owner);
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var owner = _owner();
        if (owner is null) return;

        var window = new MessageWindow(title, message);
        await window.ShowDialog(owner);
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmLabel = "OK")
    {
        var owner = _owner();
        if (owner is null) return false;

        var window = new ConfirmWindow(title, message, confirmLabel);
        return await window.ShowDialog<bool>(owner);
    }

    public async Task<VocabularySet?> ShowSetEditorAsync(VocabularySet? existing)
    {
        var owner = _owner();
        if (owner is null) return null;

        var window = new SetEditorWindow { DataContext = new SetEditorViewModel(existing) };
        return await window.ShowDialog<VocabularySet?>(owner);
    }
}
