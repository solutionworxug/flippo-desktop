using System.Text;
using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Destinations;

namespace Flippo.Tests.Cloud;

public class LocalFolderDestinationTests
{
    [Fact]
    public async Task Upload_List_Download_Delete_Roundtrip_ByteIdentical()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var dest = new LocalFolderDestination(LocalFolderConnector.BuildConfig(dir, "Test"));
            var payload = Encoding.UTF8.GetBytes("{\"schemaVersion\":1}");

            var info = await dest.UploadAsync("flippo-backup-20260707-120000.json", new MemoryStream(payload));

            var list = await dest.ListBackupsAsync();
            Assert.Single(list);
            Assert.Equal(info.RemoteId, list[0].RemoteId);
            Assert.Equal(payload.Length, list[0].SizeBytes);

            await using (var download = await dest.DownloadAsync(info.RemoteId))
            {
                using var mem = new MemoryStream();
                await download.CopyToAsync(mem);
                Assert.Equal(payload, mem.ToArray());
            }

            await dest.DeleteAsync(info.RemoteId);
            Assert.Empty(await dest.ListBackupsAsync());
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task List_MissingFolder_Throws_NotConnected()
    {
        var missing = Path.Combine(Path.GetTempPath(), "flippo-missing-" + Guid.NewGuid());
        var dest = new LocalFolderDestination(LocalFolderConnector.BuildConfig(missing, "X"));
        var ex = await Assert.ThrowsAsync<DestinationException>(() => dest.ListBackupsAsync());
        Assert.Equal(DestinationState.NotConnected, ex.State);
    }

    [Fact]
    public async Task List_IgnoresForeignFiles()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "notes.txt"), "x");
            var dest = new LocalFolderDestination(LocalFolderConnector.BuildConfig(dir, "T"));
            await dest.UploadAsync("flippo-backup-20260707-120000.json", new MemoryStream([1, 2, 3]));
            Assert.Single(await dest.ListBackupsAsync());
        }
        finally { Directory.Delete(dir, true); }
    }
}
