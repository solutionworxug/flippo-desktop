using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Core.Content;
using Flippo.Core.Domain;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>Ein noch nicht installiertes, gebündeltes Wörterbuch im Angebot.</summary>
public sealed record AvailableDict(BundledDictionaryInfo Info, string DisplayName);

/// <summary>
/// Nachschlagen-Übersicht (Port von DictionaryListScreen): installierte Wörterbücher öffnen/löschen +
/// gebündelte Wörterbücher der App-Sprache installieren.
/// </summary>
public sealed partial class DictionaryListViewModel : ViewModelBase, IActivatable
{
    private readonly UserDictionaryStore _store;
    private readonly DictionaryInstaller _installer;
    private readonly NavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly SettingsService _settings;

    public ObservableCollection<UserDictionary> Installed { get; } = new();
    public ObservableCollection<AvailableDict> Available { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasInstalled;
    [ObservableProperty] private bool _hasAvailable;
    [ObservableProperty] private bool _isEmpty;

    public DictionaryListViewModel(UserDictionaryStore store, DictionaryInstaller installer,
        NavigationService nav, IDialogService dialogs, SettingsService settings)
    {
        _store = store;
        _installer = installer;
        _nav = nav;
        _dialogs = dialogs;
        _settings = settings;
    }

    public Task OnActivatedAsync() => LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var dicts = await _store.GetDictionariesAsync();
            Installed.Clear();
            foreach (var d in dicts) Installed.Add(d);
            HasInstalled = dicts.Count > 0;

            var uiLang = _settings.Load().UiLanguage;
            var target = uiLang.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "Englisch" : "Deutsch";
            Available.Clear();
            foreach (var info in BundledDictionaries.OfferedFor(target))
                if (!dicts.Any(d => d.Name == info.SourceLanguage && d.TargetLanguage == info.TargetLanguage))
                    Available.Add(new AvailableDict(info, info.SourceLanguage));
            HasAvailable = Available.Count > 0;

            IsEmpty = !HasInstalled && !HasAvailable;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenDict(UserDictionary? d)
    {
        if (d is null) return;
        _nav.NavigateTo<UserDictionaryDetailViewModel>(vm => vm.Initialize(d.Id, d.Name));
    }

    [RelayCommand]
    private async Task InstallBundled(AvailableDict? a)
    {
        if (a is null) return;
        await _installer.InstallAsync(a.Info, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteDict(UserDictionary? d)
    {
        if (d is null) return;
        if (await _dialogs.ConfirmAsync(L.T("Dict_DeleteTitle"),
                string.Format(L.T("Dict_DeleteMsg"), d.Name), L.T("Ctx_Delete")))
        {
            await _store.DeleteDictionaryAsync(d.Id);
            await LoadAsync();
        }
    }
}
