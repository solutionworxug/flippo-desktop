using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Services;

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

    public MainWindowViewModel(NavigationService nav)
    {
        _nav = nav;
        _nav.Navigated += OnNavigated;
        _nav.NavigateTo<SetsOverviewViewModel>(clearStack: true);
    }

    public string Title => "FLIPPO Desktop";

    private void OnNavigated()
    {
        CurrentPage = _nav.Current;
        CanGoBack = _nav.CanGoBack;
    }

    [RelayCommand] private void GoBack() => _nav.GoBack();
    [RelayCommand] private void ShowSets() => _nav.NavigateTo<SetsOverviewViewModel>(clearStack: true);
    [RelayCommand] private void ShowSettings() => _nav.NavigateTo<SettingsViewModel>(clearStack: true);
}
