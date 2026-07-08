using Flippo.App.Services;

namespace Flippo.Tests.App;

public class InstalledPacksRegistryTests
{
    private static InstalledPacksRegistry NewRegistry(out string file)
    {
        file = Path.Combine(Path.GetTempPath(), $"installed-packs-{Guid.NewGuid():N}.json");
        return new InstalledPacksRegistry(file);
    }

    [Fact]
    public void MarkInstalled_Then_IsInstalled_RoundTripsAcrossInstances()
    {
        var reg = NewRegistry(out var file);
        try
        {
            Assert.False(reg.IsInstalled("en-werkzeuge"));
            reg.MarkInstalled("en-werkzeuge", 1);
            Assert.True(reg.IsInstalled("en-werkzeuge"));

            // Neue Instanz auf derselben Datei → Zustand von Disk gelesen.
            var reloaded = new InstalledPacksRegistry(file);
            Assert.True(reloaded.IsInstalled("en-werkzeuge"));
            Assert.Equal(1, reloaded.GetAll()["en-werkzeuge"]);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public void IsInstalled_UnknownId_ReturnsFalse()
    {
        var reg = NewRegistry(out var file);
        try { Assert.False(reg.IsInstalled("nope")); }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public void MarkInstalled_SameId_Overwrites_Version()
    {
        var reg = NewRegistry(out var file);
        try
        {
            reg.MarkInstalled("en-demo", 1);
            reg.MarkInstalled("en-demo", 2);
            Assert.Equal(2, reg.GetAll()["en-demo"]);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public void CorruptFile_TreatedAsEmpty_NoCrash()
    {
        var file = Path.Combine(Path.GetTempPath(), $"installed-packs-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, "{ not valid json ");
        try
        {
            var reg = new InstalledPacksRegistry(file);
            Assert.False(reg.IsInstalled("anything"));
            // Nach MarkInstalled ist die Datei repariert (gültiges JSON).
            reg.MarkInstalled("x", 1);
            Assert.True(new InstalledPacksRegistry(file).IsInstalled("x"));
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }
}
