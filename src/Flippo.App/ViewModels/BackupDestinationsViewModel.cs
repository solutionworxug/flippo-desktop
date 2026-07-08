using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Destinations;

namespace Flippo.App.ViewModels;

/// <summary>„Backup-Ziele"-Sektion der Einstellungen: Ordner- und Google-Drive-Ziele verwalten
/// (hinzufügen/entfernen, sichern/wiederherstellen, neu verbinden).</summary>
public sealed partial class BackupDestinationsViewModel : ViewModelBase
{
    private readonly DestinationStore _store;
    private readonly IFilePickerService _picker;
    private readonly IDialogService _dialogs;
    private readonly SetActionsService _actions;
    private readonly GoogleDriveConnector _gdrive;

    public ObservableCollection<DestinationConfig> Destinations { get; } = new();
    [ObservableProperty] private bool _hasDestinations;

    public BackupDestinationsViewModel(DestinationStore store, IFilePickerService picker,
        IDialogService dialogs, SetActionsService actions, GoogleDriveConnector gdrive)
    {
        _store = store;
        _picker = picker;
        _dialogs = dialogs;
        _actions = actions;
        _gdrive = gdrive;
        Reload();
    }

    private void Reload()
    {
        Destinations.Clear();
        foreach (var c in _store.GetAll()) Destinations.Add(c);
        HasDestinations = Destinations.Count > 0;
    }

    [RelayCommand]
    private async Task AddDestination()
    {
        var kind = await _dialogs.ShowProviderChooserAsync();
        switch (kind)
        {
            case BackupDestinationKind.LocalFolder:
                await AddFolderAsync();
                break;
            case BackupDestinationKind.GoogleDrive:
                await ConnectGoogleDriveAsync();
                break;
            // null = abgebrochen → nichts tun
        }
    }

    private async Task AddFolderAsync()
    {
        var path = await _picker.PickFolderAsync(L.T("Dest_PickFolderTitle"));
        if (string.IsNullOrWhiteSpace(path)) return;
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name)) name = path;
        _store.Add(LocalFolderConnector.BuildConfig(path, name));
        Reload();
    }

    private async Task ConnectGoogleDriveAsync()
    {
        try
        {
            var config = await _gdrive.ConnectInteractiveAsync();
            if (config is null) return;   // Nutzer hat OAuth abgebrochen
            _store.Add(config);
            Reload();
        }
        catch (DestinationException ex)
        {
            await _dialogs.ShowMessageAsync(L.T("Dest_ErrorTitle"), DestErrorMessage(ex));
        }
    }

    [RelayCommand]
    private async Task Reconnect(DestinationConfig? config)
    {
        if (config is null || config.Kind != BackupDestinationKind.GoogleDrive) return;
        // Neu verbinden = neues Ziel verbinden, altes ersetzen (gleicher Anzeigename-Stil).
        try
        {
            var fresh = await _gdrive.ConnectInteractiveAsync();
            if (fresh is null) return;
            _store.Remove(config.Id);
            _store.Add(fresh);
            Reload();
        }
        catch (DestinationException ex)
        {
            await _dialogs.ShowMessageAsync(L.T("Dest_ErrorTitle"), DestErrorMessage(ex));
        }
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

    private static string DestErrorMessage(DestinationException ex) => ex.State switch
    {
        DestinationState.NotConnected => L.T("Dest_ErrNotConnected"),
        DestinationState.Offline => L.T("Dest_ErrOffline"),
        DestinationState.QuotaExceeded => L.T("Dest_ErrQuota"),
        _ => L.T("Dest_ErrTransport")
    };
}

/// <summary>Kleine XAML-Konverter für die Ziel-Karten (Enum→bool).</summary>
public static class KindConverters
{
    public static readonly Avalonia.Data.Converters.IValueConverter IsGoogleDrive =
        new Avalonia.Data.Converters.FuncValueConverter<BackupDestinationKind, bool>(
            k => k == BackupDestinationKind.GoogleDrive);
}
