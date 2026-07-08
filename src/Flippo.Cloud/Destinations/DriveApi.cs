using System.Net;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using GoogleFile = Google.Apis.Drive.v3.Data.File;

namespace Flippo.Cloud.Destinations;

/// <summary>
/// Realer <see cref="IDriveApi"/> über <see cref="DriveService"/>. Übersetzt Google-/Netzwerk-Fehler
/// in <see cref="DriveApiException"/> (die Destination mappt weiter auf UI-Zustände). Scope
/// <c>drive.file</c> genügt: FLIPPO sieht nur selbst erzeugte Dateien/Ordner.
/// </summary>
public sealed class DriveApi : IDriveApi, IDisposable
{
    private const string FolderMimeType = "application/vnd.google-apps.folder";
    private const string JsonMimeType = "application/json";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly DriveService _service;

    public DriveApi(UserCredential credential)
    {
        _service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "FLIPPO"
        });
        _service.HttpClient.Timeout = Timeout;
    }

    public async Task<string> FindOrCreateFolderAsync(string name, CancellationToken ct)
    {
        return await GuardAsync(async () =>
        {
            var list = _service.Files.List();
            list.Q = $"name = '{Escape(name)}' and mimeType = '{FolderMimeType}' and trashed = false";
            list.Fields = "files(id)";
            list.Spaces = "drive";
            var found = await list.ExecuteAsync(ct);
            if (found.Files is { Count: > 0 }) return found.Files[0].Id;

            var create = _service.Files.Create(new GoogleFile { Name = name, MimeType = FolderMimeType });
            create.Fields = "id";
            var created = await create.ExecuteAsync(ct);
            return created.Id;
        });
    }

    public async Task<IReadOnlyList<DriveFile>> ListFilesAsync(string folderId, string nameContains, CancellationToken ct)
    {
        return await GuardAsync(async () =>
        {
            var list = _service.Files.List();
            list.Q = $"'{Escape(folderId)}' in parents and name contains '{Escape(nameContains)}' and trashed = false";
            list.Fields = "files(id,name,createdTime,size)";
            list.Spaces = "drive";
            var res = await list.ExecuteAsync(ct);
            IReadOnlyList<DriveFile> mapped = (res.Files ?? new List<GoogleFile>())
                .Select(ToDriveFile)
                .ToList();
            return mapped;
        });
    }

    public async Task<DriveFile> UploadAsync(string folderId, string fileName, Stream content, CancellationToken ct)
    {
        return await GuardAsync(async () =>
        {
            var meta = new GoogleFile { Name = fileName, Parents = new[] { folderId } };
            var request = _service.Files.Create(meta, content, JsonMimeType);
            request.Fields = "id,name,createdTime,size";
            var progress = await request.UploadAsync(ct);
            if (progress.Exception is not null) throw progress.Exception;
            return ToDriveFile(request.ResponseBody);
        });
    }

    public async Task<Stream> DownloadAsync(string fileId, CancellationToken ct)
    {
        return await GuardAsync(async () =>
        {
            var mem = new MemoryStream();
            await _service.Files.Get(fileId).DownloadAsync(mem, ct);
            mem.Position = 0;
            return (Stream)mem;
        });
    }

    public async Task DeleteAsync(string fileId, CancellationToken ct)
        => await GuardAsync(async () => { await _service.Files.Delete(fileId).ExecuteAsync(ct); return true; });

    public void Dispose() => _service.Dispose();

    private static DriveFile ToDriveFile(GoogleFile f) => new(
        f.Id,
        f.Name,
        f.CreatedTimeDateTimeOffset ?? DateTimeOffset.UnixEpoch,
        f.Size ?? 0);

    /// <summary>Google-Query-Escaping: einfache Anführungszeichen und Backslashes maskieren.</summary>
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");

    /// <summary>Übersetzt Google-/Netzwerk-/Timeout-Fehler in eine klassifizierte
    /// <see cref="DriveApiException"/>.</summary>
    private static async Task<T> GuardAsync<T>(Func<Task<T>> op)
    {
        try
        {
            return await op();
        }
        catch (GoogleApiException ex)
        {
            var kind = ex.HttpStatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => DriveErrorKind.Unauthorized,
                HttpStatusCode.InsufficientStorage => DriveErrorKind.QuotaExceeded,
                (HttpStatusCode)429 => DriveErrorKind.QuotaExceeded,
                _ => DriveErrorKind.Other
            };
            throw new DriveApiException(kind, ex.Message, ex);
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException or HttpRequestException)
        {
            throw new DriveApiException(DriveErrorKind.Timeout, ex.Message, ex);
        }
    }
}
