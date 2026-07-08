using System.Net;
using System.Security.Cryptography;
using System.Text;
using Flippo.Cloud.Catalog;
using Flippo.Core.Content;
using Flippo.Data.Services;

namespace Flippo.Tests.Data;

public class ThemeSetImporterCatalogTests
{
    // Minimaler In-Proc-Server (identisch zum Prinzip in CatalogClientTests, hier eigenständig).
    private sealed class FixtureServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;
        public readonly Dictionary<string, byte[]> Routes = new();
        public string BaseUrl { get; }

        public FixtureServer()
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            BaseUrl = $"http://localhost:{port}/";
            _listener.Prefixes.Add(BaseUrl);
            _listener.Start();
            _loop = Task.Run(LoopAsync);
        }

        private async Task LoopAsync()
        {
            while (true)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { return; }
                var path = ctx.Request.Url!.AbsolutePath.TrimStart('/');
                if (Routes.TryGetValue(path, out var body))
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.ContentLength64 = body.Length;
                    await ctx.Response.OutputStream.WriteAsync(body);
                }
                else ctx.Response.StatusCode = 404;
                ctx.Response.Close();
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _listener.Stop(); } catch { }
            try { _loop.Wait(TimeSpan.FromSeconds(2)); } catch { }
            _listener.Close();
            _cts.Dispose();
        }
    }

    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);
    private static string Sha256Hex(byte[] d) => Convert.ToHexString(SHA256.HashData(d)).ToLowerInvariant();

    private const string PackJson = """
        {"id":"en-demo","language":"Englisch","sourceLanguage":"Englisch","targetLanguage":"Deutsch",
         "title":"Demo","entries":[
           {"source":"cat","target":"die Katze","example":"The cat sleeps.","pos":"Substantiv","notes":"","tags":""},
           {"source":"dog","target":"der Hund","example":"The dog runs.","pos":"Substantiv","notes":"","tags":""},
           {"source":"bird","target":"der Vogel","example":"The bird sings.","pos":"Substantiv","notes":"","tags":""}]}
        """;

    private static string IndexJson(string sha) => $$"""
        {"formatVersion":1,"catalogVersion":1,"generatedAt":"2026-07-08T12:00:00Z","packs":[
          {"id":"en-demo","kind":"themeset","title":"Demo","sourceLanguage":"Englisch","targetLanguage":"Deutsch",
           "packVersion":1,"entryCount":3,"sizeBytes":123,"sha256":"{{sha}}","url":"packs/en-demo-v1.json","tags":[]}]}
        """;

    // Leere IThemeSetSource — für ImportFileAsync nicht benötigt (Datei liegt bereits vor).
    private sealed class EmptySource : IThemeSetSource
    {
        public Task<ThemeSetManifest?> LoadManifestAsync() => Task.FromResult<ThemeSetManifest?>(null);
        public Task<ThemeSetFile?> LoadFileAsync(string path) => Task.FromResult<ThemeSetFile?>(null);
    }

    [Fact]
    public async Task Catalog_DownloadPack_Then_ImportFile_CreatesSet_WithEntryCountCards()
    {
        using var db = new SqliteTestDatabase();
        using var server = new FixtureServer();
        var packBytes = Bytes(PackJson);
        server.Routes["catalog/v1/index.json"] = Bytes(IndexJson(Sha256Hex(packBytes)));
        server.Routes["catalog/v1/packs/en-demo-v1.json"] = packBytes;

        var cacheFile = Path.Combine(Path.GetTempPath(), $"cc-{Guid.NewGuid():N}.json");
        var client = new CatalogClient(server.BaseUrl, cacheFile);
        var store = new VocabularyStore(db.Factory);
        var importer = new ThemeSetImporter(new EmptySource(), store);

        try
        {
            var index = await client.GetIndexAsync(CancellationToken.None);
            var file = await client.DownloadPackAsync(index!.Packs[0], CancellationToken.None);

            var result = await importer.ImportFileAsync(file, "Demo (EN)", 1_000);

            Assert.NotNull(result);
            Assert.Equal(3, result!.EntryCount);
            var sets = await store.GetSetsWithCountsAsync(1_000);
            Assert.Contains(sets, s => s.Title == "Demo (EN)");
            var entries = await store.GetEntriesAsync(result.SetId);
            Assert.Equal(3, entries.Count);
        }
        finally { if (File.Exists(cacheFile)) File.Delete(cacheFile); }
    }

    [Fact]
    public async Task Catalog_TamperedChecksum_Refused_NoSetCreated()
    {
        using var db = new SqliteTestDatabase();
        using var server = new FixtureServer();
        var packBytes = Bytes(PackJson);
        server.Routes["catalog/v1/index.json"] = Bytes(IndexJson(Sha256Hex(Bytes("tampered"))));  // falsche sha
        server.Routes["catalog/v1/packs/en-demo-v1.json"] = packBytes;

        var cacheFile = Path.Combine(Path.GetTempPath(), $"cc-{Guid.NewGuid():N}.json");
        var client = new CatalogClient(server.BaseUrl, cacheFile);
        var store = new VocabularyStore(db.Factory);

        try
        {
            var index = await client.GetIndexAsync(CancellationToken.None);
            await Assert.ThrowsAsync<CatalogChecksumException>(
                () => client.DownloadPackAsync(index!.Packs[0], CancellationToken.None));

            // Kein Set entstanden (Download brach vor dem Import ab).
            var sets = await store.GetSetsWithCountsAsync(1_000);
            Assert.Empty(sets);
        }
        finally { if (File.Exists(cacheFile)) File.Delete(cacheFile); }
    }
}
