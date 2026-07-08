using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Flippo.Cloud.Catalog;

namespace Flippo.Tests.Cloud;

public class CatalogClientTests
{
    // ---- In-Proc-Fixture-HTTP-Server (HttpListener auf freiem localhost-Port) -------------------
    private sealed class FixtureServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        /// <summary>Pfad (ohne führenden Slash) → (Bytes, ETag|null, ContentType).</summary>
        public readonly Dictionary<string, (byte[] Body, string? ETag, string ContentType)> Routes = new();
        /// <summary>Empfangene Header je Request (für Assertions), in Reihenfolge.</summary>
        public readonly List<(string Path, string? IfNoneMatch)> Received = new();

        public string BaseUrl { get; }

        public FixtureServer()
        {
            var port = GetFreePort();
            BaseUrl = $"http://localhost:{port}/";
            _listener.Prefixes.Add(BaseUrl);
            _listener.Start();
            _loop = Task.Run(() => LoopAsync(_cts.Token));
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { return; }   // Listener gestoppt

                var path = ctx.Request.Url!.AbsolutePath.TrimStart('/');
                var ifNoneMatch = ctx.Request.Headers["If-None-Match"];
                Received.Add((path, ifNoneMatch));

                if (!Routes.TryGetValue(path, out var route))
                {
                    ctx.Response.StatusCode = 404;
                    ctx.Response.Close();
                    continue;
                }

                // ETag-Verhandlung: passt If-None-Match auf die aktuelle ETag → 304 ohne Body.
                if (route.ETag is not null && ifNoneMatch == route.ETag)
                {
                    ctx.Response.StatusCode = 304;
                    ctx.Response.Headers["ETag"] = route.ETag;
                    ctx.Response.Close();
                    continue;
                }

                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = route.ContentType;
                if (route.ETag is not null) ctx.Response.Headers["ETag"] = route.ETag;
                ctx.Response.ContentLength64 = route.Body.Length;
                await ctx.Response.OutputStream.WriteAsync(route.Body, ct);
                ctx.Response.Close();
            }
        }

        private static int GetFreePort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            try { _listener.Stop(); } catch { /* best effort */ }
            try { _loop.Wait(TimeSpan.FromSeconds(2)); } catch { /* best effort */ }
            _listener.Close();
            _cts.Dispose();
        }
    }

    private static string Sha256Hex(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    // Ein minimales, gültiges ThemeSetFile mit 2 Einträgen.
    private const string PackJson = """
        {"id":"en-demo","language":"Englisch","sourceLanguage":"Englisch","targetLanguage":"Deutsch",
         "title":"Demo","entries":[
           {"source":"cat","target":"die Katze","example":"The cat sleeps.","pos":"Substantiv","notes":"","tags":""},
           {"source":"dog","target":"der Hund","example":"The dog runs.","pos":"Substantiv","notes":"","tags":""}]}
        """;

    private static string IndexJson(string packSha) => $$"""
        {"formatVersion":1,"catalogVersion":1,"generatedAt":"2026-07-08T12:00:00Z","packs":[
          {"id":"en-demo","kind":"themeset","title":"Demo","sourceLanguage":"Englisch","targetLanguage":"Deutsch",
           "packVersion":1,"entryCount":2,"sizeBytes":123,"sha256":"{{packSha}}","url":"packs/en-demo-v1.json","tags":[]},
          {"id":"future","kind":"audiopack","title":"Future","sourceLanguage":"Englisch","targetLanguage":"Deutsch",
           "packVersion":1,"entryCount":0,"sizeBytes":0,"sha256":"00","url":"packs/future.json","tags":[]}
        ]}
        """;

    private static CatalogClient NewClient(FixtureServer server, out string cacheFile)
    {
        cacheFile = Path.Combine(Path.GetTempPath(), $"catalog-cache-{Guid.NewGuid():N}.json");
        return new CatalogClient(server.BaseUrl, cacheFile);
    }

    [Fact]
    public async Task GetIndex_Then_DownloadPack_Sha256Ok_ReturnsThemeSetFile()
    {
        using var server = new FixtureServer();
        var packBytes = Bytes(PackJson);
        var sha = Sha256Hex(packBytes);
        server.Routes["catalog/v1/index.json"] = (Bytes(IndexJson(sha)), "\"idx-1\"", "application/json");
        server.Routes["catalog/v1/packs/en-demo-v1.json"] = (packBytes, null, "application/json");

        var client = NewClient(server, out var cacheFile);
        try
        {
            var index = await client.GetIndexAsync(CancellationToken.None);
            Assert.NotNull(index);
            // Unbekanntes kind ("audiopack") wird übersprungen → nur der themeset-Pack bleibt.
            Assert.Single(index!.Packs);
            var pack = index.Packs[0];
            Assert.Equal("en-demo", pack.Id);

            var file = await client.DownloadPackAsync(pack, CancellationToken.None);
            Assert.Equal("en-demo", file.Id);
            Assert.Equal(2, file.Entries.Count);
        }
        finally { if (File.Exists(cacheFile)) File.Delete(cacheFile); }
    }

    [Fact]
    public async Task DownloadPack_TamperedChecksum_ThrowsChecksumException()
    {
        using var server = new FixtureServer();
        var packBytes = Bytes(PackJson);
        var wrongSha = Sha256Hex(Bytes("something-else"));   // passt NICHT zu packBytes
        server.Routes["catalog/v1/index.json"] = (Bytes(IndexJson(wrongSha)), "\"idx-1\"", "application/json");
        server.Routes["catalog/v1/packs/en-demo-v1.json"] = (packBytes, null, "application/json");

        var client = NewClient(server, out var cacheFile);
        try
        {
            var index = await client.GetIndexAsync(CancellationToken.None);
            var pack = index!.Packs[0];
            await Assert.ThrowsAsync<CatalogChecksumException>(
                () => client.DownloadPackAsync(pack, CancellationToken.None));
        }
        finally { if (File.Exists(cacheFile)) File.Delete(cacheFile); }
    }

    [Fact]
    public async Task GetIndex_SecondCall_SendsIfNoneMatch_And_UsesCacheOn304()
    {
        using var server = new FixtureServer();
        var packBytes = Bytes(PackJson);
        var sha = Sha256Hex(packBytes);
        server.Routes["catalog/v1/index.json"] = (Bytes(IndexJson(sha)), "\"idx-42\"", "application/json");

        var client = NewClient(server, out var cacheFile);
        try
        {
            // 1. Abruf: 200, füllt den Cache; kein If-None-Match gesendet.
            var first = await client.GetIndexAsync(CancellationToken.None);
            Assert.NotNull(first);
            Assert.Null(server.Received[0].IfNoneMatch);

            // 2. Abruf: Client sendet If-None-Match mit der gecachten ETag; Server → 304; Cache-Index dient.
            var second = await client.GetIndexAsync(CancellationToken.None);
            Assert.NotNull(second);
            Assert.Equal("\"idx-42\"", server.Received[1].IfNoneMatch);
            Assert.Single(second!.Packs);
            Assert.Equal("en-demo", second.Packs[0].Id);
        }
        finally { if (File.Exists(cacheFile)) File.Delete(cacheFile); }
    }

    [Fact]
    public async Task GetIndex_ServerUnreachable_ReturnsNull()
    {
        using var server = new FixtureServer();
        var baseUrl = server.BaseUrl;
        server.Dispose();   // Port ist frei → Verbindungsfehler

        var cacheFile = Path.Combine(Path.GetTempPath(), $"catalog-cache-{Guid.NewGuid():N}.json");
        var client = new CatalogClient(baseUrl, cacheFile);
        try
        {
            Assert.Null(await client.GetIndexAsync(CancellationToken.None));
        }
        finally { if (File.Exists(cacheFile)) File.Delete(cacheFile); }
    }

    [Fact]
    public async Task GetIndex_CorruptCache_TreatedAsNoCache()
    {
        using var server = new FixtureServer();
        var sha = Sha256Hex(Bytes(PackJson));
        server.Routes["catalog/v1/index.json"] = (Bytes(IndexJson(sha)), "\"idx-1\"", "application/json");

        var cacheFile = Path.Combine(Path.GetTempPath(), $"catalog-cache-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(cacheFile, "{ this is not valid json ");
        var client = new CatalogClient(server.BaseUrl, cacheFile);
        try
        {
            // Kaputter Cache → kein If-None-Match, sauberer 200-Fetch, Index kommt trotzdem.
            var index = await client.GetIndexAsync(CancellationToken.None);
            Assert.NotNull(index);
            Assert.Null(server.Received[0].IfNoneMatch);
        }
        finally { if (File.Exists(cacheFile)) File.Delete(cacheFile); }
    }
}
