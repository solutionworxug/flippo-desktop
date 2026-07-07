using Flippo.App.Services;
using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Destinations;

namespace Flippo.Tests.Cloud;

public class DestinationStoreTests
{
    private static DestinationStore NewStore(string file) =>
        new(new IDestinationConnector[] { new LocalFolderConnector() }, file);

    [Fact]
    public void Add_Persists_ReloadFromDisk_AndResolves()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory().FullName, "destinations.json");
        var store = NewStore(file);
        store.Add(LocalFolderConnector.BuildConfig(Path.GetTempPath(), "Backups"));

        var reloaded = NewStore(file);   // frische Instanz liest von Platte
        var all = reloaded.GetAll();
        Assert.Single(all);
        Assert.Equal("Backups", all[0].DisplayName);

        var dest = reloaded.Resolve(all[0]);
        Assert.Equal(BackupDestinationKind.LocalFolder, dest.Kind);
        Assert.Equal("Backups", dest.DisplayName);
    }

    [Fact]
    public void Remove_DeletesEntry()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory().FullName, "destinations.json");
        var store = NewStore(file);
        var cfg = LocalFolderConnector.BuildConfig(Path.GetTempPath(), "X");
        store.Add(cfg);
        store.Remove(cfg.Id);
        Assert.Empty(NewStore(file).GetAll());
    }

    [Fact]
    public void GetAll_MissingFile_ReturnsEmpty()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory().FullName, "none.json");
        Assert.Empty(NewStore(file).GetAll());
    }

    [Fact]
    public void Resolve_UnknownKind_Throws()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory().FullName, "destinations.json");
        var store = NewStore(file);
        var cfg = new DestinationConfig(Guid.NewGuid(), BackupDestinationKind.GoogleDrive, "G",
            new Dictionary<string, string>());
        Assert.Throws<InvalidOperationException>(() => store.Resolve(cfg));
    }
}
