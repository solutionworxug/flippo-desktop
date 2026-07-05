using Avalonia;
using Avalonia.Styling;

namespace Flippo.App.Services;

/// <summary>Wendet das gewählte UI-Theme an (System/Hell/Dunkel via <see cref="Application.RequestedThemeVariant"/>).</summary>
public static class ThemeService
{
    public static void Apply(string uiTheme)
    {
        if (Application.Current is null) return;
        Application.Current.RequestedThemeVariant = uiTheme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default   // "System"
        };
    }
}
