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
}
