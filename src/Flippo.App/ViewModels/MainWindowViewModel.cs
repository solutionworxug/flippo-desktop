namespace Flippo.App.ViewModels;

/// <summary>
/// Shell-ViewModel. Ab P4 hält es CurrentPage + Back-Stack (INavigationService).
/// In P0 nur ein Platzhalter-Inhalt, um Bootstrap/DI/ViewLocator zu verifizieren.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    public string Title => "FLIPPO Desktop";

    public string Greeting => "FLIPPO Desktop — Gerüst steht (P0).";
}
