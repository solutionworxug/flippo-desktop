using Flippo.Core.Domain;
using Flippo.Data.Services;

namespace Flippo.Tests.Data;

public class SettingsServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"flippo-settings-{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var svc = new SettingsService(_path);
        var s = svc.Load();
        Assert.Equal("ADAPTIVE", s.SrsMode);
        Assert.Equal(new[] { 0, 4, 7, 14, 30, 180 }, s.BoxIntervals);
        Assert.Equal(50, s.MaxCardsPerSession);
    }

    [Fact]
    public void SaveThenLoad_Roundtrips()
    {
        var svc = new SettingsService(_path);
        var original = new AppSettings
        {
            SrsMode = "FLASHCARD_BOX",
            BoxIntervals = [1, 2, 3],
            StrictAccents = true,
            LeechThreshold = 6,
            LearningDirection = "MIXED",
            UiTheme = "Dark",
            UiLanguage = "en"
        };
        svc.Save(original);

        var loaded = new SettingsService(_path).Load();
        Assert.Equal("FLASHCARD_BOX", loaded.SrsMode);
        Assert.Equal(new[] { 1, 2, 3 }, loaded.BoxIntervals);
        Assert.True(loaded.StrictAccents);
        Assert.Equal(6, loaded.LeechThreshold);
        Assert.Equal("MIXED", loaded.LearningDirection);
        Assert.Equal("Dark", loaded.UiTheme);
        Assert.Equal("en", loaded.UiLanguage);
    }

    [Fact]
    public void Save_OverwritesExistingAtomically()
    {
        var svc = new SettingsService(_path);
        svc.Save(new AppSettings { LeechThreshold = 4 });
        svc.Save(new AppSettings { LeechThreshold = 9 });   // File.Replace-Pfad

        Assert.Equal(9, new SettingsService(_path).Load().LeechThreshold);
    }

    [Fact]
    public void ToSrsSettings_MapsEnumsAndValues()
    {
        var s = SettingsService.ToSrsSettings(new AppSettings
        {
            SrsMode = "FLASHCARD_BOX",
            LearningDirection = "TARGET_TO_SOURCE",
            BoxIntervals = [0, 4, 7, 14, 30, 180]
        });
        Assert.Equal(SrsMode.FlashcardBox, s.Mode);
        Assert.Equal(LearningDirection.TargetToSource, s.LearningDirection);
    }

    [Fact]
    public void ToSrsSettings_UnknownEnum_FallsBackToDefault()
    {
        var s = SettingsService.ToSrsSettings(new AppSettings { SrsMode = "BOGUS", LearningDirection = "???" });
        Assert.Equal(SrsMode.Adaptive, s.Mode);
        Assert.Equal(LearningDirection.SourceToTarget, s.LearningDirection);
    }

    public void Dispose()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch (IOException) { }
    }
}
