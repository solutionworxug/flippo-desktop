using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Destinations;

namespace Flippo.App.ViewModels;

/// <summary>„Backup-Ziele"-Sektion der Einstellungen: Ordner-Ziele verwalten + sichern/wiederherstellen.</summary>
public sealed partial class BackupDestinationsViewModel : ViewModelBase
{
    private readonly DestinationStore _store;
    private readonly IFilePickerService _picker;
    private readonly IDialogService _dialogs;
    private readonly SetActionsService _actions;

    public ObservableCollection<DestinationConfig> Destinations { get; } = new();
    [ObservableProperty] private bool _hasDestinations;

    public BackupDestinationsViewModel(DestinationStore store, IFilePickerService picker,
        IDialogService dialogs, SetActionsService actions)
    {
        _store = store;
        _picker = picker;
        _dialogs = dialogs;
        _actions = actions;
        Reload();
    }

    private void Reload()
    {
        Destinations.Clear();
        foreach (var c in _store.GetAll()) Destinations.Add(c);
        HasDestinations = Destinations.Count > 0;
    }

    [RelayCommand]
    private async Task AddFolder()
    {
        var path = await _picker.PickFolderAsync(L.T("Dest_PickFolderTitle"));
        if (string.IsNullOrWhiteSpace(path)) return;
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name)) name = path;
        _store.Add(LocalFolderConnector.BuildConfig(path, name));
        Reload();
    }

    [RelayCommand]
    private async Task Remove(DestinationConfig? config)
    {
        if (config is null) return;
        if (await _dialogs.ConfirmAsync(L.T("Dest_RemoveTitle"),
                string.Format(L.T("Dest_RemoveMsg"), config.DisplayName), L.T("Ctx_Delete")))
        {
            _store.Remove(config.Id);
            Reload();
        }
    }

    [RelayCommand]
    private Task Backup(DestinationConfig? config)
        => config is null ? Task.CompletedTask : _actions.ExportToDestinationAsync(config);

    [RelayCommand]
    private Task Restore(DestinationConfig? config)
        => config is null ? Task.CompletedTask : _actions.RestoreFromDestinationAsync(config);
}
