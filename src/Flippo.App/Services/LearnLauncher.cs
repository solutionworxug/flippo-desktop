using Flippo.App.ViewModels;
using Flippo.Core.Session;

namespace Flippo.App.Services;

/// <summary>
/// Zentraler Lern-Starter: fragt den Lernmodus per Dialog ab und startet danach die Session.
/// Genutzt von allen Lern-Einstiegspunkten, damit die „Modus wählen → navigieren"-Logik nicht dupliziert.
/// </summary>
public sealed class LearnLauncher
{
    private readonly IDialogService _dialogs;
    private readonly NavigationService _nav;

    public LearnLauncher(IDialogService dialogs, NavigationService nav)
    {
        _dialogs = dialogs;
        _nav = nav;
    }

    /// <summary>Fragt den Lernmodus ab und startet danach die Session. Abbruch (null) → nichts.</summary>
    public async Task StartAsync(long? setId, string setName, SessionFilter filter)
    {
        var mode = await _dialogs.ShowModeChooserAsync();
        if (mode is null) return;
        _nav.NavigateTo<LearnSessionViewModel>(vm => vm.Initialize(setId, setName, filter, mode.Value));
    }
}
