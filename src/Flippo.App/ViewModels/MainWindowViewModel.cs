using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Services;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>
/// Shell/Navigations-Host. Spiegelt <see cref="NavigationService.Current"/> in eine bindbare
/// Property und stellt die Sidebar-Kommandos (Karteien | Einstellungen) + Zurück bereit.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly NavigationService _nav;

    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private bool _canGoBack;

    /// <summary>UI-Skalierung aus der Schriftgrößen-Einstellung; live aktualisierbar via <see cref="ApplyFontSize"/>.</summary>
    [ObservableProperty] private double _uiScale = 1.0;

    public MainWindowViewModel(NavigationService nav, SettingsService settings)
    {
        _nav = nav;
        UiScale = ScaleFor(settings.Load().FontSize);
        _nav.Navigated += OnNavigated;
        _nav.NavigateTo<SetsOverviewViewModel>(clearStack: true);
    }

    public string Title => "FLIPPO Desktop";

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
    [RelayCommand] private void ShowSettings() => _nav.NavigateTo<SettingsViewModel>(clearStack: true);
}
