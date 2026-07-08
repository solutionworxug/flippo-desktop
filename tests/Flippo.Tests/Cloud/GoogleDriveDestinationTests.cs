using System.Text;
using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Destinations;

namespace Flippo.Tests.Cloud;

public class GoogleDriveDestinationTests
{
    // In-Memory-Fake des Drive-Backends: ein Ordner „FLIPPO" + flache Dateiliste darin.
    private sealed class FakeDriveApi : IDriveApi
    {
        private readonly Dictionary<string, (string Name, byte[] Data, DateTimeOffset Created)> _files = new();
        private string? _folderId;
        private int _seq;

        public DriveErrorKind? FailFindFolder;
        public DriveErrorKind? FailUpload;

        public Task<string> FindOrCreateFolderAsync(string name, CancellationToken ct)
        {
            if (FailFindFolder is { } k) throw new DriveApiException(k, "find-folder failed");
            _folderId ??= "folder-" + name;
            return Task.FromResult(_folderId);
        }

        public Task<IReadOnlyList<DriveFile>> ListFilesAsync(string folderId, string nameContains, CancellationToken ct)
        {
            IReadOnlyList<DriveFile> list = _files
                .Where(kv => kv.Value.Name.Contains(nameContains, StringComparison.Ordinal))
                .Select(kv => new DriveFile(kv.Key, kv.Value.Name, kv.Value.Created, kv.Value.Data.Length))
                .ToList();
            return Task.FromResult(list);
        }

        public Task<DriveFile> UploadAsync(string folderId, string fileName, Stream content, CancellationToken ct)
        {
            if (FailUpload is { } k) throw new DriveApiException(k, "upload failed");
            using var mem = new MemoryStream();
            content.CopyTo(mem);
            var data = mem.ToArray();
            var id = "file-" + (++_seq);
            var created = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000 + _seq);
            _files[id] = (fileName, data, created);
            return Task.FromResult(new DriveFile(id, fileName, created, data.Length));
        }

        public Task<Stream> DownloadAsync(string fileId, CancellationToken ct)
            => Task.FromResult<Stream>(new MemoryStream(_files[fileId].Data, writable: false));

        public Task DeleteAsync(string fileId, CancellationToken ct)
        {
            _files.Remove(fileId);
            return Task.CompletedTask;
        }
    }

    private static GoogleDriveDestination NewDest(IDriveApi api) =>
        new(new DestinationConfig(Guid.NewGuid(), BackupDestinationKind.GoogleDrive, "Google Drive (t@x)",
            new Dictionary<string, string>()), api);

    [Fact]
    public async Task Upload_List_Download_Delete_Roundtrip_ByteIdentical()
    {
        var dest = NewDest(new FakeDriveApi());
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

    [Fact]
    public async Task List_ReturnsNewestFirst_ByNameDescending()
    {
        var dest = NewDest(new FakeDriveApi());
        await dest.UploadAsync("flippo-backup-20260101-000000.json", new MemoryStream([1]));
        await dest.UploadAsync("flippo-backup-20260102-000000.json", new MemoryStream([2]));

        var result = await dest.ListBackupsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("flippo-backup-20260102-000000.json", result[0].FileName);
        Assert.Equal("flippo-backup-20260101-000000.json", result[1].FileName);
    }

    [Fact]
    public async Task Unauthorized_MapsTo_NotConnected()
    {
        var dest = NewDest(new FakeDriveApi { FailFindFolder = DriveErrorKind.Unauthorized });
        var ex = await Assert.ThrowsAsync<DestinationException>(() => dest.ListBackupsAsync());
        Assert.Equal(DestinationState.NotConnected, ex.State);
    }

    [Fact]
    public async Task Timeout_MapsTo_Offline()
    {
        var dest = NewDest(new FakeDriveApi { FailUpload = DriveErrorKind.Timeout });
        var ex = await Assert.ThrowsAsync<DestinationException>(
            () => dest.UploadAsync("flippo-backup-20260101-000000.json", new MemoryStream([1])));
        Assert.Equal(DestinationState.Offline, ex.State);
    }

    [Fact]
    public async Task Quota_MapsTo_QuotaExceeded()
    {
        var dest = NewDest(new FakeDriveApi { FailUpload = DriveErrorKind.QuotaExceeded });
        var ex = await Assert.ThrowsAsync<DestinationException>(
            () => dest.UploadAsync("flippo-backup-20260101-000000.json", new MemoryStream([1])));
        Assert.Equal(DestinationState.QuotaExceeded, ex.State);
    }

    [Fact]
    public async Task OtherError_MapsTo_TransportFailed()
    {
        var dest = NewDest(new FakeDriveApi { FailUpload = DriveErrorKind.Other });
        var ex = await Assert.ThrowsAsync<DestinationException>(
            () => dest.UploadAsync("flippo-backup-20260101-000000.json", new MemoryStream([1])));
        Assert.Equal(DestinationState.TransportFailed, ex.State);
    }
}
