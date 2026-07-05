using System.Globalization;
using Flippo.App.Localization;

namespace Flippo.Tests.App;

/// <summary>Verifiziert den resx-Mechanismus: korrekter ResourceManager-Name + DE/EN-Auflösung.</summary>
public class LocalizationTests
{
    [Fact]
    public void T_ResolvesGerman()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        Assert.Equal("Karteien", L.T("Nav_Sets"));
        Assert.Equal("Einstellungen", L.T("Nav_Settings"));
    }

    [Fact]
    public void T_ResolvesEnglishNeutral()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        Assert.Equal("Decks", L.T("Nav_Sets"));
        Assert.Equal("Settings", L.T("Nav_Settings"));
    }

    [Fact]
    public void T_UnknownKey_ReturnsKeyItself()
    {
        Assert.Equal("Does_Not_Exist", L.T("Does_Not_Exist"));
    }

    [Fact] // Stichprobe aus dem Massen-Umbau: View-, VM- und Interpolations-Keys laden in beiden Sprachen
    public void T_ResolvesMigratedKeys()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        Assert.Equal("Speichern", L.T("Settings_Save"));
        Assert.Equal("Session beendet", L.T("Summary_Title"));
        Assert.Equal("{0} von {1} richtig · {2}%", L.T("Summary_Quote"));

        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        Assert.Equal("Save", L.T("Settings_Save"));
        Assert.Equal("Session finished", L.T("Summary_Title"));
        Assert.Equal("{0} of {1} correct · {2}%", L.T("Summary_Quote"));
    }
}
