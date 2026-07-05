using System.Globalization;
using System.Resources;

namespace Flippo.App.Localization;

/// <summary>
/// Zugriff auf die lokalisierten UI-Strings (resx). Neutral = Englisch (Strings.resx),
/// Deutsch als Variante (Strings.de.resx). Sprache wird beim App-Start gesetzt (Plan P7:
/// „wirkt nach Neustart"), daher genügt ein statischer Lookup gegen die aktuelle UI-Kultur.
/// </summary>
public static class L
{
    private static readonly ResourceManager Rm =
        new("Flippo.App.Resources.Strings", typeof(L).Assembly);

    /// <summary>Übersetzt einen Schlüssel; fehlt er, wird der Schlüssel selbst zurückgegeben (sichtbarer Hinweis).</summary>
    public static string T(string key) => Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>Setzt die UI-Sprache (z. B. "de"/"en"). Beim App-Start vor dem Laden der Views aufrufen.</summary>
    public static void SetLanguage(string language)
    {
        try { CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language); }
        catch (CultureNotFoundException) { /* unbekannt → neutrale (englische) Ressourcen */ }
    }
}
