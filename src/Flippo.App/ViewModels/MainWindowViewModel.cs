using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Core.Domain;
using Flippo.Core.Session;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>
/// Shell/Navigations-Host. Spiegelt <see cref="NavigationService.Current"/> in eine bindbare
/// Property, ist DataContext des Anwendungsmenüs (NativeMenu) und stellt die Sidebar- +
/// Menü-Kommandos bereit. Kartei-Aktionen laufen über <see cref="SetActionsService"/>.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private const string RepoUrl = "https://github.com/solutionworxug/flippo-desktop";

    private readonly NavigationService _nav;
    private readonly UpdateService _updates;
    private readonly SetActionsService _actions;
    private readonly IDialogService _dialogs;

    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private bool _canGoBack;

    /// <summary>Wird true, sobald ein Update heruntergeladen und installationsbereit ist (Banner).</summary>
    [ObservableProperty] private bool _updateReady;

    /// <summary>UI-Skalierung aus der Schriftgrößen-Einstellung; live aktualisierbar via <see cref="ApplyFontSize"/>.</summary>
    [ObservableProperty] private double _uiScale = 1.0;

    public MainWindowViewModel(NavigationService nav, SettingsService settings, UpdateService updates,
        SetActionsService actions, IDialogService dialogs)
    {
        _nav = nav;
        _updates = updates;
        _actions = actions;
        _dialogs = dialogs;
        UiScale = ScaleFor(settings.Load().FontSize);
        _nav.Navigated += OnNavigated;
        _nav.NavigateTo<SetsOverviewViewModel>(clearStack: true);
    }

    public string Title => "FLIPPO Desktop";

    /// <summary>Angezeigte App-Version im Sidebar-Footer, z.B. "v0.1.0".</summary>
    public string AppVersion => "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0");

    /// <summary>
    /// Stößt den nicht-blockierenden Update-Check an (fire-and-forget beim App-Start).
    /// Setzt <see cref="UpdateReady"/>, wenn ein Update bereitliegt.
    /// </summary>
    public async Task CheckForUpdatesAsync() => UpdateReady = await _updates.CheckAndDownloadAsync();

    [RelayCommand] private void ApplyUpdate() => _updates.ApplyAndRestart();

    /// <summary>Schriftgröße sofort anwenden (aus den Einstellungen beim Speichern).</summary>
    public void ApplyFontSize(string fontSize) => UiScale = ScaleFor(fontSize);

    private static double ScaleFor(string fontSize) => fontSize switch
    {
        "Small" => 0.9,
        "Large" => 1.15,
        _ => 1.0   // Medium
    };

    private void OnNavigated()
    {
        CurrentPage = _nav.Current;
        CanGoBack = _nav.CanGoBack;
    }

    [RelayCommand] private void GoBack() => _nav.GoBack();
    [RelayCommand] private void ShowSets() => _nav.NavigateTo<SetsOverviewViewModel>(clearStack: true);
    [RelayCommand] private void ShowStatistics() => _nav.NavigateTo<StatisticsViewModel>(clearStack: true);
    [RelayCommand] private void ShowSettings() => _nav.NavigateTo<SettingsViewModel>(clearStack: true);

    // ── Anwendungsmenü (NativeMenu) ──

    [RelayCommand] private async Task NewSet() { if (await _actions.NewSetAsync()) ShowSets(); }
    [RelayCommand] private async Task ImportFile() { if (await _actions.ImportFileAsync()) ShowSets(); }
    [RelayCommand] private async Task ImportBackup() { if (await _actions.ImportBackupAsync()) ShowSets(); }
    [RelayCommand] private Task ExportBackup() => _actions.ExportBackupAsync();

    /// <summary>"Alle fälligen lernen" aus dem Menü; Parameter = Modus (Standard Karteikarten).</summary>
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

    /// <summary>Aktualisiert die aktuelle Seite, indem ihre Aktivierung erneut ausgelöst wird.</summary>
    [RelayCommand]
    private Task Refresh() => (_nav.Current as IActivatable)?.OnActivatedAsync() ?? Task.CompletedTask;

    [RelayCommand]
    private void Quit()
        => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();

    [RelayCommand]
    private Task About()
        => _dialogs.ShowMessageAsync(L.T("Menu_AboutTitle"), string.Format(L.T("Menu_AboutBody"), AppVersion));

    [RelayCommand]
    private Task Shortcuts() => _dialogs.ShowMessageAsync(L.T("Menu_ShortcutsTitle"), L.T("Menu_ShortcutsBody"));

    [RelayCommand]
    private void OpenGitHub()
    {
        try { Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true }); }
        catch { /* kein Browser/Handler verfügbar — still ignorieren */ }
    }

    [RelayCommand]
    private async Task CheckUpdates()
    {
        UpdateReady = await _updates.CheckAndDownloadAsync();
        if (!UpdateReady)
            await _dialogs.ShowMessageAsync(L.T("Menu_NoUpdateTitle"), L.T("Menu_NoUpdateBody"));
    }
}
