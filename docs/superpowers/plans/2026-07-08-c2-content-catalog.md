# C2 — Content-Katalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nutzer öffnet den „Themensets…"-Picker und sieht zusätzlich zu den gebündelten Sets die Online-Packs aus dem Katalog (`flippo-content` auf GitHub Pages). Bundled-Einträge laden sofort (unverändert); der Katalog wird erst beim Öffnen des Pickers asynchron nachgeladen (kein Startup-Fetch). Klick auf ein Online-Pack lädt es (sha256-verifiziert) und importiert es über den bestehenden Themeset-Import-Pfad (`ImportFileAsync`) als normale Kartei. Katalog nicht erreichbar → Picker bleibt mit gebündelten Sets voll nutzbar (stiller Caption-Hinweis).

**Architecture:** `Flippo.Cloud` bekommt eine neue Katalog-Schicht `Flippo.Cloud/Catalog/` (`CatalogModels`, `CatalogClient`) — **kein neues NuGet** (HttpClient + SHA256 = BCL). `CatalogClient` ist gegen einen lokalen In-Proc-`HttpListener`-Fixture-Server testbar (Ctor nimmt Basis-URL + Cache-Dateipfad + optionalen `HttpMessageHandler`). Der `ThemeSetImporter` (Data) wird um `ImportFileAsync(ThemeSetFile, string displayTitle, long nowMs)` erweitert (verhaltens­erhaltend aus `ImportAsync` extrahiert; alte Methode delegiert). Neue App-Services: `InstalledPacksRegistry` (device-lokale `installed-packs.json`, Muster wie `DestinationStore`, NICHT im Backup). Der `ThemeSetPickerViewModel` bekommt einen zweiten, asynchronen Ladepfad, der Bundled- und Katalog-Einträge in **einer** Liste mergt (Bundle gewinnt bei id-Kollision), Online-Zeilen mit Download-Kennzeichnung + Größe zeigt und beim Klick Download → sha256 → `ImportFileAsync` → `MarkInstalled` ausführt. Neu: das öffentliche GitHub-Repo `solutionworxug/flippo-content` (Pages) als statischer Katalog-Host, geseedet per PowerShell aus den gebündelten Themesets + einem neuen Demo-Pack.

**Tech Stack:** C# / .NET 10, Avalonia 12.0.5, CommunityToolkit.Mvvm, xUnit. **Kein neues NuGet.** PowerShell für Seed-/Index-Skripte. `gh` CLI für Repo-Anlage + Pages.

## Global Constraints

- **`Flippo.Core` bleibt BCL-only und wird NICHT angefasst.** Die Katalog-Schicht liegt komplett in `Flippo.Cloud/Catalog/`; `ThemeSetFile`/`ThemeSetFileEntry` (Core) werden nur konsumiert, nicht geändert.
- **Kein neues NuGet-Paket.** `CatalogClient` nutzt ausschließlich `System.Net.Http.HttpClient` + `System.Security.Cryptography.SHA256` + `System.Text.Json` (alles BCL / bereits referenziert).
- **Pack-Format = existierendes Themeset-Format** (`ThemeSetFile`) — NICHT das Backup-Format. Import läuft durch den bestehenden `ThemeSetImporter`-Mapper (Slash-Split, Tags, PoS) via neuem `ImportFileAsync`.
- **sha256 ist Pflicht:** `CatalogClient.DownloadPackAsync` verweigert bei Checksummen-Mismatch mit einer **distinct** Exception (`CatalogChecksumException`) — laut, kein stiller Skip.
- **Offline-first, opt-in:** kein Startup-Fetch, kein Polling. Katalog wird nur beim Öffnen des Pickers geladen. Index-Timeout (15 s) / Netzfehler / korrupter Cache → `GetIndexAsync` liefert `null` (Aufrufer zeigt nur gebündelte + Caption-Hinweis).
- **Unbekannte `kind`-Werte** im Index werden vom Client übersprungen (Vorwärtskompatibilität).
- Neue UI-Strings DE **und** EN in `Strings.de.resx` + `Strings.resx` (exakte Keys + Texte in Task 4).
- **`installed-packs.json` ist device-lokal und NICHT im Backup** (wie `destinations.json`).
- Commit-Konvention: bestehende Trailer beibehalten (Implementierer hängt die Standard-Trailer des Repos an, s. `.claude`-Commit-Vorlage); niemals `.claude/` stagen; gezielt `git add` mit expliziten Pfaden; Commit-Message-Stil `C2: …`.
- `TreatWarningsAsErrors` ist an (`Directory.Build.props`) — jeder Build muss 0 Warnungen haben.
- **Repo-Anlage + GitHub Pages ist ein externer, öffentlicher Effekt** → Task 5 läuft **nur nach ausdrücklichem OK von Mark** (im Task-Text als Gate markiert).
- Erwarteter Endzustand: bestehende **213** Tests + neue C2-Tests (Task 1: 5, Task 2: 2, Task 3: 4 → **11 neue**) = **224** Tests, alle grün. **Implementierer-Verify vor Beginn:** `dotnet test` einmal laufen lassen und die exakte Ausgangszahl bestätigen (Plan geht von 213 aus, wie in der Spec §Teil 6 genannt). Falls die Ist-Zahl abweicht, die Erwartungswerte in Task 3/4 um dieselbe Differenz anpassen — die **relative** Zunahme (+11) ist die verbindliche Größe.

---

## File Structure

**Neu — `Flippo.Cloud`:**
- `src/Flippo.Cloud/Catalog/CatalogModels.cs` (DTOs `CatalogIndex`, `CatalogPack`)
- `src/Flippo.Cloud/Catalog/CatalogClient.cs` (`GetIndexAsync`, `DownloadPackAsync`, `CatalogChecksumException`)

**Neu — `Flippo.App`:**
- `src/Flippo.App/Services/InstalledPacksRegistry.cs`

**Geändert:**
- `src/Flippo.Data/AppPaths.cs` — `InstalledPacksFile` + `CatalogCacheFile`.
- `src/Flippo.Data/Services/ThemeSetImporter.cs` — `ImportFileAsync` extrahiert, `ImportAsync` delegiert.
- `src/Flippo.Data/Services/AppSettings.cs` — `CatalogBaseUrl`-Feld (Default `""`).
- `src/Flippo.App/ViewModels/ThemeSetPickerViewModel.cs` — Katalog-Merge, Online-Zeilen, Download+Import.
- `src/Flippo.App/Views/ThemeSetPickerWindow.axaml` — Online-Kennzeichnung/Größe im Item-Template, Caption-Hinweis.
- `src/Flippo.App/Services/DialogService.cs` — `ShowThemeSetPickerAsync` reicht die neuen Deps durch.
- `src/Flippo.App/App.axaml.cs` — DI: `CatalogClient` (mit Basis-URL aus Settings) + `InstalledPacksRegistry`.
- `src/Flippo.App/Resources/Strings.resx` + `Strings.de.resx` — neue `Catalog_*`-Keys.

**Tests:**
- `tests/Flippo.Tests/Cloud/CatalogClientTests.cs` (gegen In-Proc-`HttpListener`-Fixture)
- `tests/Flippo.Tests/Data/ThemeSetImporterCatalogTests.cs` (`ImportFileAsync` gegen `SqliteTestDatabase`; sha256-Refusal via `CatalogClient`)
- `tests/Flippo.Tests/App/InstalledPacksRegistryTests.cs`

**Neues Repo (Task 5, extern):** `solutionworxug/flippo-content`
- `catalog/v1/index.json`, `catalog/v1/packs/{id}-v1.json`, `tools/build-index.ps1`, `README.md`

---

## Task 1: `CatalogModels` + `CatalogClient` (TDD gegen In-Proc-Fixture-Server) [Cloud]

**Files:**
- Create: `src/Flippo.Cloud/Catalog/CatalogModels.cs`
- Create: `src/Flippo.Cloud/Catalog/CatalogClient.cs`
- Test: `tests/Flippo.Tests/Cloud/CatalogClientTests.cs`

**Interfaces:**
- Consumes: `Flippo.Core.Content.ThemeSetFile` (Pack-Deserialisierung), BCL `HttpClient`/`SHA256`/`System.Text.Json`.
- Produces: `record CatalogIndex(...)`, `record CatalogPack(...)`, `class CatalogChecksumException`, `CatalogClient(string baseUrl, string cacheFilePath, HttpMessageHandler? handler = null)` mit `GetIndexAsync(ct)` und `DownloadPackAsync(CatalogPack, ct)`.

Der Client kennt zwei Endpunkte: den Index (`{base}/catalog/v1/index.json`, mit ETag-Disk-Cache) und die einzelnen Packs (`pack.Url` relativ zur Index-Location). `GetIndexAsync` gibt bei jedem Fehler (Timeout/Netz/korrupter Cache/ungültiges JSON) `null` zurück — der Picker zeigt dann nur Bundled. `DownloadPackAsync` lädt die Pack-Bytes, prüft sha256 (hex, case-insensitive) und wirft bei Mismatch `CatalogChecksumException` (deutlich), sonst deserialisiert es als `ThemeSetFile`.

- [ ] **Step 1: Failing test schreiben (inkl. In-Proc-`HttpListener`-Fixture)**

Der Fixture-Server läuft in-proc auf einem freien localhost-Port (localhost-Prefixe brauchen keine URL-ACL). Er serviert Index + Pack-Bytes, honoriert `If-None-Match` → 304 und protokolliert empfangene Request-Header für Assertions.

Create `tests/Flippo.Tests/Cloud/CatalogClientTests.cs`:

```csharp
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

        public void Dispose()
        {
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
```

- [ ] **Step 2: Test läuft NICHT (fehlende Typen)**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~CatalogClient"`
Expected: Kompilierfehler `CatalogClient`/`CatalogIndex`/`CatalogPack`/`CatalogChecksumException` nicht gefunden.

- [ ] **Step 3: `CatalogModels` schreiben**

Create `src/Flippo.Cloud/Catalog/CatalogModels.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Flippo.Cloud.Catalog;

/// <summary>
/// Katalog-Index (`catalog/v1/index.json`), Format §Teil 2 der C2-Spec. camelCase-JSON.
/// <see cref="Packs"/> enthält nach dem Laden nur Packs mit bekanntem <c>kind</c> ("themeset");
/// unbekannte Werte werden vom <see cref="CatalogClient"/> herausgefiltert (Vorwärtskompatibilität).
/// </summary>
public sealed record CatalogIndex(
    [property: JsonPropertyName("formatVersion")] int FormatVersion,
    [property: JsonPropertyName("catalogVersion")] int CatalogVersion,
    [property: JsonPropertyName("generatedAt")] string GeneratedAt,
    [property: JsonPropertyName("packs")] IReadOnlyList<CatalogPack> Packs);

/// <summary>Ein Katalog-Pack-Eintrag. <see cref="Url"/> ist relativ zur Index-Location.</summary>
public sealed record CatalogPack(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("sourceLanguage")] string SourceLanguage,
    [property: JsonPropertyName("targetLanguage")] string TargetLanguage,
    [property: JsonPropertyName("packVersion")] int PackVersion,
    [property: JsonPropertyName("entryCount")] int EntryCount,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags);
```

- [ ] **Step 4: `CatalogClient` schreiben**

Create `src/Flippo.Cloud/Catalog/CatalogClient.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flippo.Core.Content;

namespace Flippo.Cloud.Catalog;

/// <summary>sha256 der heruntergeladenen Pack-Bytes passt nicht zum Index → Import verweigert (laut).</summary>
public sealed class CatalogChecksumException : Exception
{
    public CatalogChecksumException(string message) : base(message) { }
}

/// <summary>
/// Liest den statischen Content-Katalog (GitHub-Pages, §Teil 3). Kein neues NuGet: HttpClient + SHA256
/// (BCL). Index wird per ETag/If-None-Match gegen einen Disk-Cache (<c>catalog-cache.json</c>) validiert.
/// Jeder Index-Fehler (Timeout 15 s / Netz / korrupter Cache / ungültiges JSON) → <c>null</c>, damit der
/// Picker offline nur die gebündelten Sets zeigt. Pack-Download prüft sha256 zwingend.
/// </summary>
public sealed class CatalogClient
{
    private const string IndexPath = "catalog/v1/index.json";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Uri _indexUri;
    private readonly string _cacheFilePath;
    private readonly HttpClient _http;

    /// <param name="baseUrl">z.B. <c>https://solutionworxug.github.io/flippo-content/</c> (Slash egal).</param>
    /// <param name="cacheFilePath">Disk-Cache <c>{ etag, indexJson }</c>.</param>
    /// <param name="handler">Optional für Tests; sonst Default-Handler.</param>
    public CatalogClient(string baseUrl, string cacheFilePath, HttpMessageHandler? handler = null)
    {
        var baseUri = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
        _indexUri = new Uri(baseUri, IndexPath);
        _cacheFilePath = cacheFilePath;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = Timeout;
    }

    /// <summary>Disk-Cache-Form: die rohe Index-JSON plus die ETag, mit der sie geladen wurde.</summary>
    private sealed record CacheEntry(string Etag, string IndexJson);

    /// <summary>
    /// Lädt den Index. Sendet <c>If-None-Match</c>, wenn der Cache eine ETag hat. 200 → Cache aktualisieren
    /// und parsen; 304 → gecachte Index-JSON parsen; jeder Fehler → <c>null</c>. Filtert unbekannte
    /// <c>kind</c>-Werte heraus.
    /// </summary>
    public async Task<CatalogIndex?> GetIndexAsync(CancellationToken ct)
    {
        var cache = ReadCache();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _indexUri);
            if (cache is not null)
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(cache.Etag));

            using var response = await _http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NotModified && cache is not null)
                return Parse(cache.IndexJson);

            if (!response.IsSuccessStatusCode)
                return cache is not null ? Parse(cache.IndexJson) : null;

            var json = await response.Content.ReadAsStringAsync(ct);
            var etag = response.Headers.ETag?.ToString();
            if (etag is not null)
                WriteCache(new CacheEntry(etag, json));
            return Parse(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // Netz/Timeout: falls ein Cache existiert, ihn servieren, sonst null.
            return cache is not null ? Parse(cache.IndexJson) : null;
        }
    }

    /// <summary>
    /// Lädt die Pack-Datei (<see cref="CatalogPack.Url"/> relativ zur Index-Location), prüft sha256
    /// (hex, case-insensitiv) gegen <see cref="CatalogPack.Sha256"/> und deserialisiert als
    /// <see cref="ThemeSetFile"/>. Mismatch → <see cref="CatalogChecksumException"/>.
    /// </summary>
    public async Task<ThemeSetFile> DownloadPackAsync(CatalogPack pack, CancellationToken ct)
    {
        var packUri = new Uri(_indexUri, pack.Url);
        var bytes = await _http.GetByteArrayAsync(packUri, ct);

        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(actual, pack.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new CatalogChecksumException(
                $"sha256-Mismatch für Pack '{pack.Id}': erwartet {pack.Sha256}, berechnet {actual}.");

        var file = JsonSerializer.Deserialize<ThemeSetFile>(bytes);
        return file ?? throw new CatalogChecksumException($"Pack '{pack.Id}' ist leer/ungültig.");
    }

    /// <summary>Parst Index-JSON und filtert Packs mit unbekanntem <c>kind</c> (nur "themeset" bleibt).</summary>
    private static CatalogIndex? Parse(string json)
    {
        try
        {
            var index = JsonSerializer.Deserialize<CatalogIndex>(json, JsonOptions);
            if (index is null) return null;
            var known = index.Packs
                .Where(p => string.Equals(p.Kind, "themeset", StringComparison.OrdinalIgnoreCase))
                .ToList();
            return index with { Packs = known };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private CacheEntry? ReadCache()
    {
        if (!File.Exists(_cacheFilePath)) return null;
        try
        {
            var raw = File.ReadAllText(_cacheFilePath);
            var entry = JsonSerializer.Deserialize<CacheEntry>(raw, JsonOptions);
            return entry is null || string.IsNullOrEmpty(entry.Etag) ? null : entry;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;   // korrupter Cache → wie „kein Cache" behandeln
        }
    }

    private void WriteCache(CacheEntry entry)
    {
        try
        {
            var dir = Path.GetDirectoryName(_cacheFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_cacheFilePath, JsonSerializer.Serialize(entry, JsonOptions), Encoding.UTF8);
        }
        catch (IOException) { /* Cache ist best effort — Fehlschlag ist unkritisch */ }
    }
}
```

> **Implementierer-Verify:** `EntityTagHeaderValue` erwartet einen ETag **inklusive Anführungszeichen** (z.B. `"idx-42"`). Der Fixture-Server setzt die ETag exakt so, und `response.Headers.ETag?.ToString()` liefert sie wieder mit Quotes — Round-Trip stimmt. Falls ein realer Server eine „weak" ETag (`W/"…"`) liefert, akzeptiert `EntityTagHeaderValue.Parse` das ebenfalls; der Cache speichert den `ToString()`-Wert unverändert, daher bleibt der Vergleich konsistent.

- [ ] **Step 5: Test läuft grün**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~CatalogClient"`
Expected: PASS (5 Tests).

- [ ] **Step 6: Commit**

```bash
git add src/Flippo.Cloud/Catalog/CatalogModels.cs src/Flippo.Cloud/Catalog/CatalogClient.cs tests/Flippo.Tests/Cloud/CatalogClientTests.cs
git commit -m "C2: CatalogModels + CatalogClient (ETag-Cache, sha256-Verify, unbekanntes kind gefiltert; TDD gegen In-Proc-Fixture)"
```

---

## Task 2: `ThemeSetImporter.ImportFileAsync`-Refactor + `AppPaths`-Ergänzungen [Data]

**Files:**
- Modify: `src/Flippo.Data/Services/ThemeSetImporter.cs`
- Modify: `src/Flippo.Data/AppPaths.cs`
- Test: `tests/Flippo.Tests/Data/ThemeSetImporterCatalogTests.cs`

**Interfaces:**
- Consumes: `ThemeSetFile`, `VocabularyStore`, `ColumnMapping`, `ImportEngine`.
- Produces: `ThemeSetImporter.ImportFileAsync(ThemeSetFile file, string displayTitle, long nowMs) : Task<ThemeSetImportResult?>`; `AppPaths.InstalledPacksFile`, `AppPaths.CatalogCacheFile`.

Der Datei-Import-Teil (Titel-Dedupe → AddSet → Mapping → AddEntries) wird aus `ImportAsync` **verhaltens­erhaltend** in `ImportFileAsync` extrahiert. `ImportAsync` lädt weiterhin die Datei über die `IThemeSetSource` und delegiert dann an `ImportFileAsync` — so bleibt der bundled Pfad byte-gleich, und der Katalog-Pfad (bereits geladenes `ThemeSetFile`) nutzt dieselbe Logik.

- [ ] **Step 1: Failing test schreiben**

Der Test nutzt `SqliteTestDatabase` (echte DB, kein InMemory) und den `CatalogClient` gegen einen kleinen In-Proc-Fixture-Server, um den **kompletten** Spec-Pfad zu prüfen: Index → Pack → sha256-ok → `ImportFileAsync` erzeugt ein Set mit `entryCount` Karten; und: tampered checksum → kein Set. Der Fixture-Server wird als kleine lokale Kopie eingebettet (unabhängig von Task 1, damit dieser Test eigenständig lauffähig bleibt).

Create `tests/Flippo.Tests/Data/ThemeSetImporterCatalogTests.cs`:

```csharp
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Flippo.App.Services;                 // BundledThemeSetSource nicht nötig — eigener Fake unten
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
```

> **Implementierer-Verify:** Der `using Flippo.App.Services;` oben ist ungenutzt (er stand nur als Hinweis) — vor dem Grün-Machen entfernen, sonst schlägt `TreatWarningsAsErrors` bei CS8019 (unnötiges using) fehl. Der Test braucht `Flippo.App` NICHT (kein Referenz-Zwang); falls das Test-Projekt `Flippo.App` ohnehin referenziert, ist das ok, aber das `using` bleibt trotzdem zu streichen.

- [ ] **Step 2: Test läuft NICHT (fehlende Methode)**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~ThemeSetImporterCatalog"`
Expected: Kompilierfehler `ImportFileAsync` nicht gefunden.

- [ ] **Step 3: `ImportFileAsync` extrahieren, `ImportAsync` delegiert**

Replace `src/Flippo.Data/Services/ThemeSetImporter.cs`:

```csharp
using Flippo.Core.Content;
using Flippo.Core.Domain;
using Flippo.Core.Import;

namespace Flippo.Data.Services;

/// <summary>Ergebnis eines Themenset-Imports.</summary>
public sealed record ThemeSetImportResult(long SetId, string Title, int EntryCount);

/// <summary>
/// Port von ThemeSetRepository.importAsSet: ein Themenset wird als normale Kartei mit Karten importiert.
/// Duplikat-Check über den Anzeigetitel; kein Update-/Merge-Pfad (wie Android). Kein Drip, kein Free-Limit
/// (Desktop ist kostenlos), keine Wörterbuch-Kopplung. Nutzt den vorhandenen P9-Mapper. Der Datei-Teil ist
/// als <see cref="ImportFileAsync"/> extrahiert, damit der C2-Online-Katalog dieselbe Import-Mechanik nutzt.
/// </summary>
public sealed class ThemeSetImporter
{
    private readonly IThemeSetSource _source;
    private readonly VocabularyStore _store;

    public ThemeSetImporter(IThemeSetSource source, VocabularyStore store)
    {
        _source = source;
        _store = store;
    }

    /// <summary>Verfügbare Themensets für eine Zielsprache ("Deutsch"/"Englisch").</summary>
    public async Task<IReadOnlyList<ThemeSetManifestEntry>> GetAvailableAsync(string targetLanguage)
    {
        var manifest = await _source.LoadManifestAsync();
        if (manifest is null) return [];
        return manifest.ThemeSets
            .Where(t => string.Equals(t.TargetLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Importiert ein gebündeltes Themenset unter <paramref name="displayTitle"/>. Rückgabe <c>null</c> =
    /// bereits importiert (Titel existiert) oder Datei nicht ladbar/leer. Lädt die Datei und delegiert an
    /// <see cref="ImportFileAsync"/> (verhaltensgleich zur vorherigen Inline-Logik).
    /// </summary>
    public async Task<ThemeSetImportResult?> ImportAsync(ThemeSetManifestEntry entry, string displayTitle, long nowMs)
    {
        var file = await _source.LoadFileAsync(entry.Path);
        if (file is null) return null;
        return await ImportFileAsync(file, displayTitle, nowMs);
    }

    /// <summary>
    /// Importiert ein bereits geladenes <see cref="ThemeSetFile"/> unter <paramref name="displayTitle"/> als
    /// normale Kartei. Rückgabe <c>null</c> = bereits importiert (Titel existiert) oder Datei leer. Titel-Dedupe
    /// gegen bestehende Karteien inklusive. Gemeinsamer Pfad für gebündelte Sets und Online-Packs (C2).
    /// </summary>
    public async Task<ThemeSetImportResult?> ImportFileAsync(ThemeSetFile file, string displayTitle, long nowMs)
    {
        if (file.Entries.Count == 0) return null;

        var sets = await _store.GetSetsWithCountsAsync(nowMs);
        if (sets.Any(s => string.Equals(s.Title, displayTitle, StringComparison.OrdinalIgnoreCase)))
            return null;   // bereits importiert

        long setId = await _store.AddSetAsync(new VocabularySet
        {
            Title = displayTitle,
            SourceLanguage = file.SourceLanguage,
            TargetLanguage = file.TargetLanguage,
            CreatedAt = nowMs,
            UpdatedAt = nowMs
        });

        // Zeilen [source, target, example, notes, tags, pos] durch den P9-Mapper
        // (" / "->acceptedAnswers, Tags ;/,). Keine Kopfzeile.
        var rows = file.Entries
            .Select(e => (IReadOnlyList<string>)new[] { e.Source, e.Target, e.Example, e.Notes, e.Tags, e.Pos })
            .ToList();
        var mapping = new ColumnMapping
        {
            SourceTextColumn = 0, TargetTextColumn = 1, ExampleSentenceColumn = 2,
            NotesColumn = 3, TagsColumn = 4, PartOfSpeechColumn = 5, SplitAlternatives = true
        };
        var (mapped, _) = ImportEngine.MapToEntries(rows, setId, mapping, nowMs, treatFirstRowAsHeader: false);
        await _store.AddEntriesAsync(mapped);

        return new ThemeSetImportResult(setId, displayTitle, mapped.Count);
    }
}
```

> **Verhaltens-Hinweis:** Die frühere `ImportAsync`-Reihenfolge war „Dedupe-Check → LoadFile → leer?→null". Neu ist „LoadFile → (in ImportFileAsync) leer?→null → Dedupe-Check". Für den bundled Pfad ist das Ergebnis identisch (beide Zweige liefern `null`, nur die Prüf-Reihenfolge unterscheidet sich; es gibt keine Seiteneffekte vor dem `AddSetAsync`). Die bestehenden Themeset-Import-Tests bleiben grün (in Step 5 verifiziert).

- [ ] **Step 4: `AppPaths` um `InstalledPacksFile` + `CatalogCacheFile` ergänzen**

In `src/Flippo.Data/AppPaths.cs` nach der `TokensDirectory`-Zeile einfügen:

```csharp
    public static string InstalledPacksFile => Path.Combine(DataDirectory, "installed-packs.json");
    public static string CatalogCacheFile => Path.Combine(DataDirectory, "catalog-cache.json");
```

(Beide liegen im Datenverzeichnis, sind device-lokal und **nicht** Teil des Backups — wie `destinations.json`. Keine `EnsureDirectories`-Änderung nötig: es sind Dateien im bereits angelegten `DataDirectory`.)

- [ ] **Step 5: Test läuft grün + bestehende Themeset-Tests grün**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~ThemeSetImporter"`
Expected: PASS — die 2 neuen `ThemeSetImporterCatalog`-Tests **und** alle bestehenden `ThemeSetImporter`-Tests grün.

- [ ] **Step 6: Commit**

```bash
git add src/Flippo.Data/Services/ThemeSetImporter.cs src/Flippo.Data/AppPaths.cs tests/Flippo.Tests/Data/ThemeSetImporterCatalogTests.cs
git commit -m "C2: ThemeSetImporter.ImportFileAsync extrahiert (Katalog nutzt Import-Pfad) + AppPaths InstalledPacksFile/CatalogCacheFile"
```

---

## Task 3: `InstalledPacksRegistry` (TDD) [App]

**Files:**
- Create: `src/Flippo.App/Services/InstalledPacksRegistry.cs`
- Test: `tests/Flippo.Tests/App/InstalledPacksRegistryTests.cs`

**Interfaces:**
- Consumes: `AppPaths.InstalledPacksFile`, `System.Text.Json`.
- Produces: `InstalledPacksRegistry(string? filePath = null)` mit `bool IsInstalled(string id)`, `void MarkInstalled(string id, int version)`, `IReadOnlyDictionary<string,int> GetAll()`.

Muster wie `DestinationStore`: persistiert eine Map `packId → packVersion` in `installed-packs.json`; fehlende/korrupte Datei → leer (kein Crash). Device-lokal, nicht im Backup.

- [ ] **Step 1: Failing test schreiben**

Create `tests/Flippo.Tests/App/InstalledPacksRegistryTests.cs`:

```csharp
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
```

- [ ] **Step 2: Test läuft NICHT (fehlender Typ)**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~InstalledPacksRegistry"`
Expected: Kompilierfehler `InstalledPacksRegistry` nicht gefunden.

> **Hinweis:** Falls das Verzeichnis `tests/Flippo.Tests/App/` noch nicht existiert, legt der Write der Test-Datei es an. Der Namespace `Flippo.Tests.App` ist neu, aber zulässig (xUnit entdeckt Tests namespace-unabhängig).

- [ ] **Step 3: `InstalledPacksRegistry` implementieren**

Create `src/Flippo.App/Services/InstalledPacksRegistry.cs`:

```csharp
using System.Text.Json;
using Flippo.Data;

namespace Flippo.App.Services;

/// <summary>
/// Merkt sich device-lokal, welche Katalog-Packs importiert wurden (Map <c>packId → packVersion</c>) in
/// <c>installed-packs.json</c>. Muster wie <see cref="DestinationStore"/>: fehlende/korrupte Datei → leer,
/// kein Crash. <b>Nicht im Backup</b> (Gerätezustand, keine Lerninhalte — importierte Packs sind normale
/// Karteien und werden über den regulären Backup-Pfad gesichert). Kein Update-Pfad in C2: neuere
/// packVersions werden ignoriert; die Registry dient nur der „Importiert"-Kennzeichnung im Picker.
/// </summary>
public sealed class InstalledPacksRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private Dictionary<string, int> _map;

    public InstalledPacksRegistry(string? filePath = null)
    {
        _filePath = filePath ?? AppPaths.InstalledPacksFile;
        _map = Load();
    }

    /// <summary>True, wenn das Pack (per Id) bereits importiert wurde.</summary>
    public bool IsInstalled(string id) => _map.ContainsKey(id);

    /// <summary>Merkt das Pack als importiert (überschreibt eine frühere Version) und persistiert.</summary>
    public void MarkInstalled(string id, int version)
    {
        _map[id] = version;
        Persist();
    }

    /// <summary>Schnappschuss aller importierten Packs (Id → Version).</summary>
    public IReadOnlyDictionary<string, int> GetAll() => new Dictionary<string, int>(_map);

    private Dictionary<string, int> Load()
    {
        if (!File.Exists(_filePath)) return new Dictionary<string, int>();
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json, JsonOptions)
                   ?? new Dictionary<string, int>();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new Dictionary<string, int>();   // korrupt/unlesbar → leer behandeln
        }
    }

    private void Persist()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(_map, JsonOptions));
    }
}
```

- [ ] **Step 4: Test läuft grün**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~InstalledPacksRegistry"`
Expected: PASS (4 Tests).

- [ ] **Step 5: Commit**

```bash
git add src/Flippo.App/Services/InstalledPacksRegistry.cs tests/Flippo.Tests/App/InstalledPacksRegistryTests.cs
git commit -m "C2: InstalledPacksRegistry (installed-packs.json, device-lokal, nicht im Backup; TDD)"
```

---

## Task 4: Picker-Integration + resx + DI + `AppSettings.CatalogBaseUrl` [App, Build + volle Suite]

**Files:**
- Modify: `src/Flippo.Data/Services/AppSettings.cs` (`CatalogBaseUrl`)
- Modify: `src/Flippo.App/ViewModels/ThemeSetPickerViewModel.cs` (Merge + Online-Zeilen + Download/Import)
- Modify: `src/Flippo.App/Views/ThemeSetPickerWindow.axaml` (Online-Kennzeichnung/Größe + Caption)
- Modify: `src/Flippo.App/Services/DialogService.cs` (`ShowThemeSetPickerAsync` reicht Deps durch)
- Modify: `src/Flippo.App/Services/SetActionsService.cs` (Aufruf reicht Deps durch)
- Modify: `src/Flippo.App/App.axaml.cs` (DI: `CatalogClient` + `InstalledPacksRegistry`)
- Modify: `src/Flippo.App/Resources/Strings.resx` + `Strings.de.resx`

**Interfaces:**
- Consumes: `CatalogClient`, `CatalogPack`, `CatalogChecksumException`, `InstalledPacksRegistry`, `ThemeSetImporter.ImportFileAsync`, `IThemeSetSource` (bundled ids für Merge-Dedupe).
- Produces: erweitertes `ThemeSetPickerViewModel` (Bundled sofort, Katalog async, Merge, Online-Import); `AppSettings.CatalogBaseUrl`; DI-Registrierungen.

**Design:** Der Picker lädt wie bisher **sofort** die gebündelten Einträge (`LoadAsync`). Danach startet er einen zweiten, asynchronen Ladepfad (`LoadCatalogAsync`), der den Index holt. Bundled ids werden aus dem Manifest gesammelt; Online-Packs mit kollidierender id werden ausgeblendet. Online-Zeilen tragen `IsOnline=true`, eine Größenangabe und (falls Registry oder Titel-Dedupe) den „Importiert"-Zustand. Klick auf eine Online-Zeile: `DownloadPackAsync` → `ImportFileAsync` → `MarkInstalled` → Zeile flippt auf „Importiert". Bei `CatalogChecksumException` oder Download-Fehler: Meldung, kein Import. Ist der Katalog nicht erreichbar (`GetIndexAsync` == null): stiller Caption-Hinweis, Picker voll nutzbar. Der Sprachfilter (UI-Sprache → Zielsprache) gilt auch für Online-Packs: nur Packs mit passender `TargetLanguage` werden gemerged (die Sprach-Facette bleibt `SourceLanguage`, wie bei bundled `Language`).

- [ ] **Step 1: `AppSettings.CatalogBaseUrl` ergänzen**

In `src/Flippo.Data/Services/AppSettings.cs` in der UI-Sektion (nach `UiLanguage`) einfügen:

```csharp
    // Katalog-Basis-URL (C2). Leer = eingebaute Default-URL (siehe CatalogClient-DI). Kein UI dafür;
    // macht Fixture-Tests und einen späteren Host-Umzug trivial.
    public string CatalogBaseUrl { get; init; } = "";
```

- [ ] **Step 2: resx-Keys (EN + DE)**

In `src/Flippo.App/Resources/Strings.resx` vor `</root>` einfügen:

```xml
  <data name="Catalog_OnlineTag" xml:space="preserve"><value>Online</value></data>
  <data name="Catalog_DownloadTag" xml:space="preserve"><value>Download · {0}</value></data>
  <data name="Catalog_Download" xml:space="preserve"><value>Download</value></data>
  <data name="Catalog_Unreachable" xml:space="preserve"><value>Catalog unavailable — showing bundled sets only.</value></data>
  <data name="Catalog_ErrorTitle" xml:space="preserve"><value>Download failed</value></data>
  <data name="Catalog_ShaMismatch" xml:space="preserve"><value>The downloaded pack is corrupt (checksum mismatch). Import was refused.</value></data>
  <data name="Catalog_DownloadError" xml:space="preserve"><value>The pack could not be downloaded. Please try again later.</value></data>
```

In `src/Flippo.App/Resources/Strings.de.resx` vor `</root>` einfügen:

```xml
  <data name="Catalog_OnlineTag" xml:space="preserve"><value>Online</value></data>
  <data name="Catalog_DownloadTag" xml:space="preserve"><value>Herunterladen · {0}</value></data>
  <data name="Catalog_Download" xml:space="preserve"><value>Herunterladen</value></data>
  <data name="Catalog_Unreachable" xml:space="preserve"><value>Katalog nicht erreichbar – es werden nur gebündelte Sets angezeigt.</value></data>
  <data name="Catalog_ErrorTitle" xml:space="preserve"><value>Download fehlgeschlagen</value></data>
  <data name="Catalog_ShaMismatch" xml:space="preserve"><value>Das heruntergeladene Pack ist beschädigt (Prüfsummen-Fehler). Der Import wurde verweigert.</value></data>
  <data name="Catalog_DownloadError" xml:space="preserve"><value>Das Pack konnte nicht heruntergeladen werden. Bitte später erneut versuchen.</value></data>
```

- [ ] **Step 3: `ThemeSetPickerViewModel` erweitern**

Replace `src/Flippo.App/ViewModels/ThemeSetPickerViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Cloud.Catalog;
using Flippo.Core.Content;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>
/// Ein Themenset in der Picker-Liste. <see cref="IsImported"/> steuert den Button-Zustand; <see cref="IsOnline"/>
/// unterscheidet gebündelte (sofort importierbar) von Online-Packs (erst herunterladen). Bei Online-Packs trägt
/// <see cref="Pack"/> die Katalog-Metadaten (inkl. sha256/url), <see cref="Entry"/> ist dann <c>null</c>.
/// </summary>
public sealed partial class ThemeSetItem : ObservableObject
{
    public ThemeSetManifestEntry? Entry { get; }
    public CatalogPack? Pack { get; }
    public string Title { get; }
    public string CountText { get; }
    public bool IsOnline { get; }
    /// <summary>Für Online-Zeilen: „Herunterladen · 4,7 KB"; für bundled leer.</summary>
    public string DownloadText { get; }
    [ObservableProperty] private bool _isImported;

    /// <summary>Gebündeltes Set.</summary>
    public ThemeSetItem(ThemeSetManifestEntry entry, string title, string countText)
    {
        Entry = entry;
        Title = title;
        CountText = countText;
        IsOnline = false;
        DownloadText = "";
    }

    /// <summary>Online-Pack aus dem Katalog.</summary>
    public ThemeSetItem(CatalogPack pack, string title, string countText, string downloadText)
    {
        Pack = pack;
        Title = title;
        CountText = countText;
        IsOnline = true;
        DownloadText = downloadText;
    }
}

/// <summary>
/// Themenset-Picker (P12 + C2-Katalog): Sprachfilter + Liste, Inline-Import. Gebündelte Sets laden sofort;
/// der Online-Katalog wird beim Öffnen asynchron nachgeladen und in dieselbe Liste gemergt (Bundle gewinnt
/// bei id-Kollision). Online-Packs zeigen eine Download-Kennzeichnung + Größe; Klick lädt sha256-verifiziert
/// und importiert über <see cref="ThemeSetImporter.ImportFileAsync"/>. Katalog nicht erreichbar → stiller
/// Hinweis, Picker voll nutzbar.
/// </summary>
public sealed partial class ThemeSetPickerViewModel : ViewModelBase
{
    private readonly ThemeSetImporter _importer;
    private readonly IThemeSetSource _bundledSource;
    private readonly CatalogClient _catalog;
    private readonly InstalledPacksRegistry _installed;
    private readonly IDialogService _dialogs;
    private readonly string _targetLanguage;

    private List<ThemeSetManifestEntry> _bundled = new();
    private List<CatalogPack> _onlinePacks = new();
    private HashSet<string> _bundledIds = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<string> Languages { get; } = new();
    public ObservableCollection<ThemeSetItem> Items { get; } = new();

    [ObservableProperty] private string _selectedLanguage = "";
    [ObservableProperty] private bool _isEmpty;
    /// <summary>True → Caption „Katalog nicht erreichbar" wird sichtbar (Picker bleibt nutzbar).</summary>
    [ObservableProperty] private bool _catalogUnreachable;

    /// <summary>True, sobald mindestens ein Set importiert wurde → Aufrufer lädt die Karteien neu.</summary>
    public bool AnyImported { get; private set; }

    public ThemeSetPickerViewModel(ThemeSetImporter importer, IThemeSetSource bundledSource,
        CatalogClient catalog, InstalledPacksRegistry installed, IDialogService dialogs, string targetLanguage)
    {
        _importer = importer;
        _bundledSource = bundledSource;
        _catalog = catalog;
        _installed = installed;
        _dialogs = dialogs;
        _targetLanguage = targetLanguage;
    }

    /// <summary>Lädt sofort die gebündelten Sets (unverändert). Der Katalog kommt in <see cref="LoadCatalogAsync"/>.</summary>
    public async Task LoadAsync()
    {
        _bundled = (await _importer.GetAvailableAsync(_targetLanguage)).ToList();

        var manifest = await _bundledSource.LoadManifestAsync();
        _bundledIds = manifest is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : manifest.ThemeSets.Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        RebuildLanguages();
        SelectedLanguage = Languages[0];   // "Alle Sprachen"
        Apply();
    }

    /// <summary>
    /// Holt den Online-Katalog (nutzergetriggert beim Öffnen; kein Startup-Fetch). Nicht erreichbar → Caption.
    /// Online-Packs, deren id gebündelt ist, werden verworfen (Bundle gewinnt).
    /// </summary>
    public async Task LoadCatalogAsync(CancellationToken ct = default)
    {
        CatalogIndex? index;
        try { index = await _catalog.GetIndexAsync(ct); }
        catch { index = null; }

        if (index is null)
        {
            CatalogUnreachable = true;
            return;
        }

        _onlinePacks = index.Packs
            .Where(p => !_bundledIds.Contains(p.Id))
            .Where(p => string.Equals(p.TargetLanguage, _targetLanguage, StringComparison.OrdinalIgnoreCase))
            .ToList();

        RebuildLanguages();
        Apply();
    }

    partial void OnSelectedLanguageChanged(string value) => Apply();

    private void RebuildLanguages()
    {
        var current = SelectedLanguage;
        Languages.Clear();
        Languages.Add(L.T("ThemeSet_AllLanguages"));
        var langs = _bundled.Select(e => e.Language)
            .Concat(_onlinePacks.Select(p => p.SourceLanguage))
            .Distinct()
            .OrderBy(x => x);
        foreach (var lang in langs) Languages.Add(lang);
        if (!string.IsNullOrEmpty(current) && Languages.Contains(current)) SelectedLanguage = current;
    }

    private void Apply()
    {
        Items.Clear();
        bool all = string.IsNullOrEmpty(SelectedLanguage) || SelectedLanguage == L.T("ThemeSet_AllLanguages");

        var bundledItems = (all ? _bundled : _bundled.Where(e => e.Language == SelectedLanguage))
            .OrderBy(e => e.Title)
            .Select(e => new ThemeSetItem(e, BundledTitle(e), string.Format(L.T("ThemeSet_CardCount"), e.EntryCount)));
        foreach (var item in bundledItems) Items.Add(item);

        var onlineItems = (all ? _onlinePacks : _onlinePacks.Where(p => p.SourceLanguage == SelectedLanguage))
            .OrderBy(p => p.Title)
            .Select(CreateOnlineItem);
        foreach (var item in onlineItems) Items.Add(item);

        IsEmpty = Items.Count == 0;
    }

    private ThemeSetItem CreateOnlineItem(CatalogPack pack)
    {
        var title = $"{CatalogTitle(pack)} ({pack.SourceLanguage})";
        var count = string.Format(L.T("ThemeSet_CardCount"), pack.EntryCount);
        var download = string.Format(L.T("Catalog_DownloadTag"), FormatSize(pack.SizeBytes));
        var item = new ThemeSetItem(pack, title, count, download);
        // Bereits importiert? Registry (per id) oder Titel-Dedupe (gleicher Anzeigetitel schon als Kartei).
        if (_installed.IsInstalled(pack.Id)) item.IsImported = true;
        return item;
    }

    [RelayCommand]
    private async Task Import(ThemeSetItem? item)
    {
        if (item is null || item.IsImported) return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (!item.IsOnline)
        {
            var result = await _importer.ImportAsync(item.Entry!, item.Title, now);
            item.IsImported = true;   // auch bei „bereits vorhanden" (null) als importiert markieren
            if (result is not null) AnyImported = true;
            return;
        }

        // Online: Download → sha256 (im Client) → ImportFileAsync → Registry.
        var pack = item.Pack!;
        try
        {
            var file = await _catalog.DownloadPackAsync(pack, CancellationToken.None);
            var result = await _importer.ImportFileAsync(file, item.Title, now);
            _installed.MarkInstalled(pack.Id, pack.PackVersion);
            item.IsImported = true;
            if (result is not null) AnyImported = true;
        }
        catch (CatalogChecksumException)
        {
            await _dialogs.ShowMessageAsync(L.T("Catalog_ErrorTitle"), L.T("Catalog_ShaMismatch"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            await _dialogs.ShowMessageAsync(L.T("Catalog_ErrorTitle"), L.T("Catalog_DownloadError"));
        }
    }

    /// <summary>Lokalisierter Themen-Titel eines gebündelten Sets (Fallback: Manifest-Titel).</summary>
    private static string BundledTitle(ThemeSetManifestEntry e)
    {
        var key = "ThemeSetTitle_" + e.Topic.Replace('-', '_');
        var loc = L.T(key);
        var title = loc == key ? e.Title : loc;
        return $"{title} ({e.Language})";
    }

    /// <summary>Lokalisierter Titel eines Online-Packs (Topic aus der id, wie bei bundled).</summary>
    private static string CatalogTitle(CatalogPack p)
    {
        int dash = p.Id.IndexOf('-');
        var topic = dash >= 0 && dash < p.Id.Length - 1 ? p.Id[(dash + 1)..] : p.Id;
        var key = "ThemeSetTitle_" + topic.Replace('-', '_');
        var loc = L.T(key);
        return loc == key ? p.Title : loc;
    }

    /// <summary>Menschliche Größenangabe (KB) für die Download-Kennzeichnung.</summary>
    private static string FormatSize(long bytes)
    {
        double kb = bytes / 1024.0;
        return kb < 1024 ? $"{kb:0.#} KB" : $"{kb / 1024:0.#} MB";
    }
}
```

> **Implementierer-Verify:** `HttpRequestException` liegt in `System.Net.Http`; mit `ImplicitUsings=enable` ist der Namespace bereits verfügbar. Falls nicht auflösbar, `using System.Net.Http;` oben ergänzen.

- [ ] **Step 4: `ThemeSetPickerWindow.axaml` — Online-Kennzeichnung/Größe + Caption**

Replace den Inhalt von `src/Flippo.App/Views/ThemeSetPickerWindow.axaml` (Struktur bleibt; das Item-Template bekommt eine Online-Zeile und ein Download-Button-Label, plus die Unreachable-Caption):

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Flippo.App.ViewModels"
        xmlns:loc="using:Flippo.App.Localization"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        x:Class="Flippo.App.Views.ThemeSetPickerWindow"
        x:DataType="vm:ThemeSetPickerViewModel"
        Title="{loc:T ThemeSet_PickerTitle}"
        Width="520" Height="620"
        Background="{DynamicResource Brush.Bg.App}"
        WindowStartupLocation="CenterOwner">

    <DockPanel Margin="24">
        <TextBlock DockPanel.Dock="Top" Classes="page-title" Text="{loc:T ThemeSet_Heading}"/>
        <ComboBox DockPanel.Dock="Top" ItemsSource="{Binding Languages}" SelectedItem="{Binding SelectedLanguage}"
                  Margin="0,12,0,12" HorizontalAlignment="Left" MinWidth="200"/>

        <Button DockPanel.Dock="Bottom" Content="{loc:T ThemeSet_Close}" Click="OnClose"
                HorizontalAlignment="Right" Margin="0,12,0,0" MinWidth="90"/>

        <TextBlock DockPanel.Dock="Bottom" IsVisible="{Binding CatalogUnreachable}" Classes="caption"
                   Text="{loc:T Catalog_Unreachable}" Margin="2,8,0,0"/>
        <TextBlock DockPanel.Dock="Bottom" IsVisible="{Binding IsEmpty}" Classes="caption"
                   Text="{loc:T ThemeSet_Empty}" Margin="2,8,0,0"/>

        <ScrollViewer>
            <ItemsControl ItemsSource="{Binding Items}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate x:DataType="vm:ThemeSetItem">
                        <Border Classes="app-card" Margin="0,0,0,8" Padding="14">
                            <Grid ColumnDefinitions="*,Auto">
                                <StackPanel Grid.Column="0" Spacing="2" VerticalAlignment="Center">
                                    <TextBlock Classes="body" FontWeight="SemiBold" Text="{Binding Title}"/>
                                    <TextBlock Classes="caption" Text="{Binding CountText}"/>
                                    <TextBlock Classes="caption" Text="{Binding DownloadText}"
                                               Foreground="{DynamicResource Brush.Accent}"
                                               IsVisible="{Binding IsOnline}"/>
                                </StackPanel>
                                <Button Grid.Column="1" VerticalAlignment="Center" Classes="accent"
                                        IsVisible="{Binding !IsImported}"
                                        Command="{Binding $parent[ItemsControl].((vm:ThemeSetPickerViewModel)DataContext).ImportCommand}"
                                        CommandParameter="{Binding}">
                                    <Button.Content>
                                        <TextBlock Text="{loc:T ThemeSet_Import}" IsVisible="{Binding !IsOnline}"/>
                                    </Button.Content>
                                </Button>
                                <TextBlock Grid.Column="1" Classes="caption" Text="{loc:T ThemeSet_Imported}" VerticalAlignment="Center"
                                           Foreground="{DynamicResource Brush.Success}" IsVisible="{Binding IsImported}"/>
                            </Grid>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>
    </DockPanel>
</Window>
```

> **Verify (Button-Label):** Bundled-Zeilen zeigen „Importieren" (`ThemeSet_Import`), Online-Zeilen einen Download-Button. Um für Online den Download-Text zu zeigen, die `Button.Content` um ein zweites `TextBlock` ergänzen (der Implementierer wählt die einfachste Form, die kompiliert):
> ```xml
> <TextBlock Text="{loc:T Catalog_Download}" IsVisible="{Binding IsOnline}"/>
> ```
> beide TextBlocks liegen dann in einem `<Panel>` innerhalb `Button.Content` (nur eines ist je Zeile sichtbar). Alternativ, falls das im Blend-Preview zickt: statt zwei TextBlocks einen einzelnen mit einem `FuncValueConverter<bool,string>` — die zwei-TextBlock-Variante ist aber ohne neuen Converter und bevorzugt. Nach der Änderung `Brush.Accent` verifizieren: existiert es nicht als DynamicResource, `Brush.Success` oder `Brush.Text.Secondary` nutzen (im Theme-Resource-Dictionary prüfen).

- [ ] **Step 5: `DialogService.ShowThemeSetPickerAsync` — neue Deps durchreichen**

Der Dialog-Service konstruiert das ViewModel und muss die neuen Abhängigkeiten (bundled source, catalog, installed, dialogs) übergeben und nach `LoadAsync` den Katalog anstoßen. Signatur wird erweitert.

In `src/Flippo.App/Services/DialogService.cs` das Interface `IDialogService` ändern:

Alt:
```csharp
    /// <summary>Themenset-Picker (P12). Rückgabe true, wenn mindestens ein Set importiert wurde.</summary>
    Task<bool> ShowThemeSetPickerAsync(ThemeSetImporter importer, string targetLanguage);
```
Neu:
```csharp
    /// <summary>Themenset-Picker (P12 + C2-Katalog). Rückgabe true, wenn mindestens ein Set importiert wurde.</summary>
    Task<bool> ShowThemeSetPickerAsync(ThemeSetImporter importer, IThemeSetSource bundledSource,
        CatalogClient catalog, InstalledPacksRegistry installed, string targetLanguage);
```

Und die Implementierung in `DialogService`:

Alt:
```csharp
    public async Task<bool> ShowThemeSetPickerAsync(ThemeSetImporter importer, string targetLanguage)
    {
        var owner = _owner();
        if (owner is null) return false;

        var vm = new ThemeSetPickerViewModel(importer, targetLanguage);
        await vm.LoadAsync();
        var window = new ThemeSetPickerWindow { DataContext = vm };
        await window.ShowDialog(owner);
        return vm.AnyImported;
    }
```
Neu:
```csharp
    public async Task<bool> ShowThemeSetPickerAsync(ThemeSetImporter importer, IThemeSetSource bundledSource,
        CatalogClient catalog, InstalledPacksRegistry installed, string targetLanguage)
    {
        var owner = _owner();
        if (owner is null) return false;

        var vm = new ThemeSetPickerViewModel(importer, bundledSource, catalog, installed, this, targetLanguage);
        await vm.LoadAsync();                       // gebündelte Sets sofort
        var window = new ThemeSetPickerWindow { DataContext = vm };
        _ = vm.LoadCatalogAsync();                  // Katalog async nachladen (nicht blockierend)
        await window.ShowDialog(owner);
        return vm.AnyImported;
    }
```

> **Implementierer-Verify:** Oben in `DialogService.cs` die Usings ergänzen, falls nicht vorhanden: `using Flippo.Cloud.Catalog;` und `using Flippo.Core.Content;` (für `IThemeSetSource`). `this` ist als `IDialogService` an das VM übergeben — passt zum VM-Ctor-Parameter `IDialogService dialogs`.

- [ ] **Step 6: `SetActionsService.ImportThemeSetAsync` — neue Deps durchreichen**

`SetActionsService` hält bereits `_themeSets` (ThemeSetImporter). Es braucht zusätzlich `IThemeSetSource`, `CatalogClient` und `InstalledPacksRegistry` im Ctor, um sie an den Dialog zu reichen.

In `src/Flippo.App/Services/SetActionsService.cs`:

(a) Usings oben ergänzen:
```csharp
using Flippo.Cloud.Catalog;
using Flippo.Core.Content;
```

(b) Felder + Ctor erweitern. Alte Felder-/Ctor-Region:
```csharp
    private readonly ThemeSetImporter _themeSets;
    private readonly SettingsService _settings;
    private readonly CloudBackupService _cloud;
    private readonly DestinationStore _destinations;

    public SetActionsService(VocabularyStore store, IFilePickerService filePicker, IDialogService dialogs,
        BackupService backup, FileImportService fileImport, ThemeSetImporter themeSets, SettingsService settings,
        CloudBackupService cloud, DestinationStore destinations)
    {
        _store = store;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _backup = backup;
        _fileImport = fileImport;
        _themeSets = themeSets;
        _settings = settings;
        _cloud = cloud;
        _destinations = destinations;
    }
```
Neu:
```csharp
    private readonly ThemeSetImporter _themeSets;
    private readonly IThemeSetSource _bundledSource;
    private readonly CatalogClient _catalog;
    private readonly InstalledPacksRegistry _installed;
    private readonly SettingsService _settings;
    private readonly CloudBackupService _cloud;
    private readonly DestinationStore _destinations;

    public SetActionsService(VocabularyStore store, IFilePickerService filePicker, IDialogService dialogs,
        BackupService backup, FileImportService fileImport, ThemeSetImporter themeSets, IThemeSetSource bundledSource,
        CatalogClient catalog, InstalledPacksRegistry installed, SettingsService settings,
        CloudBackupService cloud, DestinationStore destinations)
    {
        _store = store;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _backup = backup;
        _fileImport = fileImport;
        _themeSets = themeSets;
        _bundledSource = bundledSource;
        _catalog = catalog;
        _installed = installed;
        _settings = settings;
        _cloud = cloud;
        _destinations = destinations;
    }
```

(c) `ImportThemeSetAsync` anpassen. Alt:
```csharp
        return _dialogs.ShowThemeSetPickerAsync(_themeSets, target);
```
Neu:
```csharp
        return _dialogs.ShowThemeSetPickerAsync(_themeSets, _bundledSource, _catalog, _installed, target);
```

- [ ] **Step 7: DI — `CatalogClient` + `InstalledPacksRegistry`**

In `src/Flippo.App/App.axaml.cs` in `ConfigureServices` **nach** `services.AddSingleton<ThemeSetImporter>();` ergänzen (Usings oben ergänzen: `using Flippo.Cloud.Catalog;`):

```csharp
        services.AddSingleton<InstalledPacksRegistry>();
        services.AddSingleton(sp =>
        {
            // Basis-URL: Override aus Settings (leer = eingebauter Default). Kein UI dafür.
            const string DefaultCatalogBaseUrl = "https://solutionworxug.github.io/flippo-content/";
            var configured = sp.GetRequiredService<SettingsService>().Load().CatalogBaseUrl;
            var baseUrl = string.IsNullOrWhiteSpace(configured) ? DefaultCatalogBaseUrl : configured;
            return new CatalogClient(baseUrl, AppPaths.CatalogCacheFile);
        });
```

> **Implementierer-Verify:** `SetActionsService` ist als Singleton registriert; DI löst den erweiterten Ctor auf, sobald `IThemeSetSource`, `CatalogClient`, `InstalledPacksRegistry` registriert sind (alle drei sind es: `IThemeSetSource` bereits, die zwei neuen hier). Reihenfolge der `AddSingleton`-Aufrufe ist für den DI-Container egal.

- [ ] **Step 8: Build + volle Testsuite grün**

Run: `dotnet build src/Flippo.App/Flippo.App.csproj -c Debug`
Expected: 0 Warnungen, 0 Fehler.

Run: `dotnet test`
Expected: PASS — **224** Tests grün (213 bestehende + 5 CatalogClient + 2 ThemeSetImporterCatalog + 4 InstalledPacksRegistry), 0 Fehler. (Falls die Ist-Basis von 213 abweicht — s. Global Constraints — muss die Summe um dieselbe Differenz abweichen; die +11 sind fix.)

- [ ] **Step 9: Commit**

```bash
git add src/Flippo.Data/Services/AppSettings.cs src/Flippo.App/ViewModels/ThemeSetPickerViewModel.cs src/Flippo.App/Views/ThemeSetPickerWindow.axaml src/Flippo.App/Services/DialogService.cs src/Flippo.App/Services/SetActionsService.cs src/Flippo.App/App.axaml.cs src/Flippo.App/Resources/Strings.resx src/Flippo.App/Resources/Strings.de.resx
git commit -m "C2: Picker-Katalog-Integration (Bundled+Online-Merge, Download/sha256/Import, Registry-State) + resx + DI + AppSettings.CatalogBaseUrl"
```

---

## Task 5: Repo `solutionworxug/flippo-content` — Seed + build-index.ps1 + Demo-Pack + gh create/Pages/Verify [EXTERN — Marks OK nötig]

> **⚠ APPROVAL-GATE:** Dieser Task legt ein **öffentliches** GitHub-Repo an und schaltet GitHub Pages (öffentlich erreichbarer Content) frei — ein externer, öffentlich sichtbarer Effekt. **Vor Ausführung von Step 4 (gh) muss Mark ausdrücklich zustimmen.** Steps 1–3 (lokale Datei-/Skript-Erstellung) sind vorbereitend und ohne externen Effekt; sie dürfen vorab laufen.

**Files (im NEUEN Repo `flippo-content`, außerhalb des Haupt-Repos):**
- Create: `catalog/v1/packs/{id}-v1.json` (aus allen gebündelten Themesets + 1 Demo-Pack)
- Create: `catalog/v1/index.json` (durch `build-index.ps1` generiert)
- Create: `tools/build-index.ps1`
- Create: `README.md`

**Arbeitsverzeichnis:** ein Ordner **außerhalb** von `FLIPPO-Desktop`, z.B. `D:/Claude/flippo-content`. Nichts davon wird ins Haupt-Repo committet.

- [ ] **Step 1: Repo-Ordner + Seed-Skript (bundled Themesets kopieren)**

Erstelle lokal `D:/Claude/flippo-content/` und darin ein Seed-Skript. Es kopiert alle Themeset-JSONs aus dem App-Assets-Ordner (außer `manifest.json`) nach `catalog/v1/packs/{id}-v1.json`, wobei die `{id}` aus dem `id`-Feld **jeder Datei selbst** stammt.

Create `D:/Claude/flippo-content/tools/seed-from-bundle.ps1`:

```powershell
# Kopiert alle gebündelten Themesets (außer manifest.json) als versionierte Pack-Dateien in den Katalog.
# ID kommt aus dem 'id'-Feld JEDER Datei (nicht aus dem Dateinamen), damit Pack-Dateiname == {id}-v1.json.
param(
    [string]$BundleRoot = "D:/Claude/Obsidian/FLIPPO-Desktop/src/Flippo.App/Assets/ThemeSets",
    [string]$PacksDir   = "D:/Claude/flippo-content/catalog/v1/packs"
)

New-Item -ItemType Directory -Force -Path $PacksDir | Out-Null

$files = Get-ChildItem -Path $BundleRoot -Recurse -Filter *.json |
         Where-Object { $_.Name -ne "manifest.json" }

$copied = 0
foreach ($f in $files) {
    $json = Get-Content -Raw -Encoding UTF8 $f.FullName | ConvertFrom-Json
    $id = $json.id
    if ([string]::IsNullOrWhiteSpace($id)) {
        Write-Warning "Keine 'id' in $($f.FullName) — übersprungen."
        continue
    }
    $dest = Join-Path $PacksDir "$id-v1.json"
    Copy-Item -Path $f.FullName -Destination $dest -Force
    $copied++
}
Write-Host "Kopiert: $copied Pack-Dateien nach $PacksDir"
```

Run: `powershell -File D:/Claude/flippo-content/tools/seed-from-bundle.ps1`
Expected: „Kopiert: N Pack-Dateien …" (N = Anzahl gebündelter Themesets, ~240).

- [ ] **Step 2: Demo-Pack „Werkzeuge" verbatim anlegen (Topic NICHT im Bundle)**

„werkzeuge" kommt im gebündelten `manifest.json` **nicht** vor (verifiziert: nur adjektive, farben, tierwelt, … — kein Werkzeug-Topic). Die id `en-werkzeuge` kollidiert daher nicht mit Bundle-ids → das Pack ist im Picker als „nur online" sichtbar.

Create `D:/Claude/flippo-content/catalog/v1/packs/en-werkzeuge-v1.json`:

```json
{
  "id": "en-werkzeuge",
  "language": "Englisch",
  "sourceLanguage": "Englisch",
  "targetLanguage": "Deutsch",
  "title": "Werkzeuge",
  "entries": [
    { "source": "hammer", "target": "der Hammer", "example": "He hit the nail with a hammer.", "pos": "Substantiv", "notes": "", "tags": "" },
    { "source": "screwdriver", "target": "der Schraubenzieher", "example": "Use a screwdriver to tighten the screw.", "pos": "Substantiv", "notes": "", "tags": "" },
    { "source": "screw", "target": "die Schraube", "example": "The screw is too long.", "pos": "Substantiv", "notes": "", "tags": "" },
    { "source": "nail", "target": "der Nagel", "example": "The nail bent when I hit it.", "pos": "Substantiv", "notes": "", "tags": "" },
    { "source": "saw", "target": "die Säge", "example": "The saw cuts through wood easily.", "pos": "Substantiv", "notes": "", "tags": "" },
    { "source": "drill", "target": "die Bohrmaschine", "example": "The drill needs a new battery.", "pos": "Substantiv", "notes": "", "tags": "" },
    { "source": "wrench", "target": "der Schraubenschlüssel", "example": "I need a wrench for this bolt.", "pos": "Substantiv", "notes": "", "tags": "" },
    { "source": "pliers", "target": "die Zange", "example": "Grab the wire with the pliers.", "pos": "Substantiv", "notes": "", "tags": "" },
    { "source": "tape measure", "target": "das Maßband", "example": "Check the length with a tape measure.", "pos": "Substantiv", "notes": "", "tags": "" },
    { "source": "level", "target": "die Wasserwaage", "example": "Use a level to hang the shelf straight.", "pos": "Substantiv", "notes": "", "tags": "" },
    { "source": "toolbox", "target": "der Werkzeugkasten", "example": "The toolbox is under the bench.", "pos": "Substantiv", "notes": "", "tags": "" },
    { "source": "sandpaper", "target": "das Schleifpapier", "example": "Smooth the edge with sandpaper.", "pos": "Substantiv", "notes": "", "tags": "" },
    { "source": "chisel", "target": "der Meißel", "example": "He carved the wood with a chisel.", "pos": "Substantiv", "notes": "", "tags": "" },
    { "source": "nut", "target": "die Mutter", "example": "Tighten the nut onto the bolt.", "pos": "Substantiv", "notes": "Technik-Kontext: 'nut' = Mutter (Schraube), nicht die Nuss.", "tags": "" },
    { "source": "bolt", "target": "der Bolzen", "example": "The bolt holds the two plates together.", "pos": "Substantiv", "notes": "", "tags": "" }
  ]
}
```

(15 Einträge, EN→DE, fachlich korrekt; `pos`/`example` im `ThemeSetFile`-Format wie die bundled Files.)

- [ ] **Step 3: `build-index.ps1` schreiben + Index bauen**

Das Skript scannt `packs/`, berechnet je Datei sha256 (hex, lowercase), sizeBytes und entryCount (aus dem JSON) und schreibt `catalog/v1/index.json` im Spec-Format. `title/sourceLanguage/targetLanguage/id` kommen aus der Pack-Datei; `packVersion` aus dem Dateinamen-Suffix `-v{n}`; `kind` fix „themeset"; `url` relativ (`packs/{file}`); `tags` leer.

Create `D:/Claude/flippo-content/tools/build-index.ps1`:

```powershell
# Baut catalog/v1/index.json aus allen Pack-Dateien unter catalog/v1/packs/.
# Manuell bei Content-Änderungen ausführen (kein CI — YAGNI).
param(
    [string]$CatalogRoot = "D:/Claude/flippo-content/catalog/v1"
)

$packsDir = Join-Path $CatalogRoot "packs"
$indexPath = Join-Path $CatalogRoot "index.json"

$packs = @()
foreach ($f in (Get-ChildItem -Path $packsDir -Filter *.json | Sort-Object Name)) {
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    $sha = [System.BitConverter]::ToString(
        [System.Security.Cryptography.SHA256]::HashData($bytes)).Replace("-", "").ToLowerInvariant()

    $json = Get-Content -Raw -Encoding UTF8 $f.FullName | ConvertFrom-Json

    # packVersion aus dem Dateinamen-Suffix "-v{n}.json"; Fallback 1.
    $packVersion = 1
    if ($f.BaseName -match '-v(\d+)$') { $packVersion = [int]$Matches[1] }

    $packs += [ordered]@{
        id             = $json.id
        kind           = "themeset"
        title          = $json.title
        sourceLanguage = $json.sourceLanguage
        targetLanguage = $json.targetLanguage
        packVersion    = $packVersion
        entryCount     = @($json.entries).Count
        sizeBytes      = $bytes.Length
        sha256         = $sha
        url            = "packs/$($f.Name)"
        tags           = @()
    }
}

$index = [ordered]@{
    formatVersion  = 1
    catalogVersion = 1
    generatedAt    = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    packs          = $packs
}

# UTF-8 ohne BOM schreiben (GitHub Pages / Content-Type application/json).
$outJson = $index | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($indexPath, $outJson, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Index geschrieben: $indexPath ($($packs.Count) Packs)"
```

Run: `powershell -File D:/Claude/flippo-content/tools/build-index.ps1`
Expected: „Index geschrieben: … (N+1 Packs)" — N bundled + 1 Demo-Pack.
Verify: `catalog/v1/index.json` enthält einen Eintrag `"id":"en-werkzeuge"` mit einer nicht-leeren `sha256` und `entryCount: 15`.

> **Implementierer-Verify:** `[System.Security.Cryptography.SHA256]::HashData([byte[]])` ist ein statischer .NET-Member (verfügbar unter Windows PowerShell 5.1 mit .NET 4.8? — **prüfen**). Falls `HashData` in der PowerShell-Runtime fehlt (nur ab neuerem .NET), stattdessen `Get-FileHash -Algorithm SHA256 $f.FullName` nutzen und `.Hash.ToLowerInvariant()` verwenden — das ist der robustere Weg und in Windows PowerShell 5.1 immer vorhanden. Der Implementierer wählt die Variante, die in seiner Shell läuft; das Ergebnis (lowercase hex) muss identisch sein.

- [ ] **Step 4: `README.md` + Repo anlegen + Pages + Verify [EXTERN — Marks OK]**

**Nur nach Marks ausdrücklichem OK ausführen.** Erst `README.md` schreiben, dann Repo öffentlich anlegen, seeden, pushen, Pages aktivieren, Live-URL verifizieren.

Create `D:/Claude/flippo-content/README.md`:

```markdown
# flippo-content

Statischer Content-Katalog für **FLIPPO Desktop** (C2). Kein Server-Code — GitHub Pages liefert
`catalog/v1/index.json` + versionierte, immutable Pack-Dateien (`catalog/v1/packs/{id}-v{n}.json`)
im Themeset-Format. Der Index wird manuell mit `tools/build-index.ps1` neu gebaut.

- Basis-URL: https://solutionworxug.github.io/flippo-content/
- Index: https://solutionworxug.github.io/flippo-content/catalog/v1/index.json

## Neuen Content einspielen
1. Pack-JSON (Themeset-Format) nach `catalog/v1/packs/{id}-v{n}.json` legen (versioniert, nie überschreiben).
2. `powershell -File tools/build-index.ps1` ausführen.
3. `index.json` + neue Packs committen und pushen.
```

Dann (Arbeitsverzeichnis `D:/Claude/flippo-content`):

```bash
git init -b main
git add catalog tools README.md
git commit -m "C2: flippo-content Katalog (Seed aus Bundle + Werkzeuge-Demo-Pack + build-index.ps1)"

# Öffentliches Repo anlegen und pushen (GATE: Marks OK):
gh repo create solutionworxug/flippo-content --public --source=. --remote=origin --push
```

GitHub Pages aktivieren (classic branch-serving, `main` / root). **Der Implementierer verifiziert den exakten `gh api`-Call und passt ihn an**, falls die API-Form abweicht:

```bash
gh api -X POST repos/solutionworxug/flippo-content/pages -f "source[branch]=main" -f "source[path]=/"
```

> **Implementierer-Verify (Pages-API):** Die klassische Pages-Aktivierung erwartet ein `source`-Objekt mit `branch` + `path`. Die obige `-f "source[branch]=main"`-Form ist die dokumentierte Schreibweise; falls `gh api` sie ablehnt, alternativ per JSON-Body: `echo '{"source":{"branch":"main","path":"/"}}' | gh api -X POST repos/solutionworxug/flippo-content/pages --input -`. Wenn Pages im Repo bereits existiert, liefert POST 409 → dann `PUT` statt `POST` verwenden. Nicht die Workflow-Variante (`build_type=workflow`) nutzen — hier reicht klassisches Branch-Serving.

Verify (Live-URL, nach 1–2 min CDN-Propagation):

```bash
curl -sSI https://solutionworxug.github.io/flippo-content/catalog/v1/index.json
```
Expected: `HTTP/2 200` **und** ein `etag:`-Header (Voraussetzung für den If-None-Match-Pfad des Clients). Zusätzlich:
```bash
curl -sS https://solutionworxug.github.io/flippo-content/catalog/v1/index.json | head -c 200
```
Expected: gültiges JSON, beginnend mit `{"formatVersion":1,...` und enthält `"id":"en-werkzeuge"`.

- [ ] **Step 5: (Kein Haupt-Repo-Commit)**

Dieser Task committet **nur im neuen `flippo-content`-Repo**. Im Haupt-Repo `FLIPPO-Desktop` entsteht kein Commit (nichts zu stagen). Ergebnis (Repo-URL + Live-Verify) protokollieren.

---

## Task 6: Manueller Live-E2E (Plan-C2-Gate) [Mark]

> Nicht automatisierbar (echter Netzabruf gegen GitHub Pages + UI). Abnahme-Gate des Slices. Kein Code-Commit; Ergebnis wird protokolliert. **Vorbedingung:** Task 5 abgeschlossen (Repo live, `index.json` liefert 200 + ETag).

- [ ] **Step 1: App bauen und starten**

Run: `dotnet build src/Flippo.App/Flippo.App.csproj -c Debug`
Expected: 0 Warnungen, 0 Fehler. Dann App starten.

- [ ] **Step 2: Demo-Pack (nur online) im Picker sichtbar**

Karteien → „Themensets…" öffnen (UI-Sprache Deutsch → Zielsprache „Deutsch"). Erwartung: Neben den gebündelten Sets erscheint kurz verzögert die Zeile **„Werkzeuge (Englisch)"** mit Online-Kennzeichnung „Herunterladen · … KB".
- Verify (Beweis): Die Werkzeuge-Zeile ist da und trägt die Download-Kennzeichnung (nicht den normalen „Importieren"-Button der bundled Sets).

- [ ] **Step 3: Download → Import**

Auf die Werkzeuge-Zeile „Herunterladen" klicken. Erwartung: Zeile flippt auf „Importiert ✓".
- Verify (Beweis): In der Karteien-Übersicht existiert eine neue Kartei „Werkzeuge (Englisch)" mit 15 Karten.

- [ ] **Step 4: „Importiert"-Zustand persistiert**

Picker schließen und erneut öffnen. Erwartung: Werkzeuge-Zeile zeigt sofort „Importiert ✓" (aus `installed-packs.json`).
- Verify (Beweis): `%APPDATA%\FLIPPO\installed-packs.json` enthält `"en-werkzeuge": 1`.

- [ ] **Step 5: Offline-Verhalten**

WLAN aus → Picker schließen und neu öffnen. Erwartung: nur die gebündelten Sets, dazu die Caption „Katalog nicht erreichbar – …" (`Catalog_Unreachable`), Picker voll bedienbar, keine Exception. WLAN wieder an.
- Verify (Beweis): Caption sichtbar; bundled Import funktioniert weiterhin.

- [ ] **Step 6: Ergebnis protokollieren**

Alle 5 Schritte mit Beweis abgehakt → C2-Gate bestanden.

---

## Self-Review

**Spec-Abdeckung (jede Spec-Sektion → Task):**
- §Constraints (offline-first/opt-in, kein Startup-Fetch; `Flippo.Core` BCL-only; kein neues NuGet; Pack=Themeset-Format via `ThemeSetImporter`; sha256 Pflicht; resx DE+EN; Repo=externer Effekt mit OK) → Task 1 (Client BCL, sha256, kein NuGet), Task 2 (Import-Pfad), Task 4 (kein Startup-Fetch: `LoadCatalogAsync` erst nach `LoadAsync` beim Öffnen; resx), Task 5 (Repo-Gate). ✓
- §Teil 1 (Repo-Struktur, Seed = bundled + 1 Demo-Pack, build-index.ps1, Pages) → Task 5 (Steps 1–4: seed-from-bundle.ps1, Werkzeuge-Pack, build-index.ps1, gh create/Pages/verify). ✓
- §Teil 2 (Index-Format, camelCase, `url` relativ, unbekanntes `kind` übersprungen) → Task 1 (`CatalogModels` camelCase; `CatalogClient.Parse` filtert `kind != themeset`; Test „audiopack" wird verworfen) + Task 5 (build-index.ps1 schreibt exakt dieses Format). ✓
- §Teil 3 (`CatalogModels`, `CatalogClient` Ctor Basis-URL+Cache, `GetIndexAsync` mit If-None-Match/304/Timeout→null, `DownloadPackAsync` sha256→`ThemeSetFile`, kein NuGet) → Task 1 vollständig (Ctor `(baseUrl, cacheFilePath, handler?)`, ETag-Cache, `CatalogChecksumException`). ✓
- §Teil 4 (`InstalledPacksRegistry` Muster DestinationStore, nicht im Backup; `ImportFileAsync`-Extraktion; Picker-Merge instant-bundled + async-catalog, id-Dedupe, Online-Kennzeichnung+Größe, Klick→Download→sha256→ImportFileAsync→Registry→„Importiert", unreachable→Caption; Sprachfilter auch online; Basis-URL-Konstante mit AppSettings-Override) → Task 3 (Registry), Task 2 (`ImportFileAsync`), Task 4 (VM-Merge, Online-Zeilen, Download/Import, Caption, Sprachfilter, `CatalogBaseUrl`+DI-Default). ✓
- §Teil 5 (Fehlerbilder: Index-Timeout→still+Caption; Pack-Download-Fehler→Meldung; sha256-Mismatch→verweigert+deutliche Meldung; kaputter Cache→wie kein Cache) → Task 1 (Client: null bei Timeout, `CatalogChecksumException`, korrupter Cache-Test) + Task 4 (VM: `Catalog_ShaMismatch`/`Catalog_DownloadError`-Meldungen, `Catalog_Unreachable`-Caption). ✓
- §Teil 6 (Tests: (1) index→pack→sha256-ok→`ImportFileAsync` erzeugt Set mit entryCount Karten über echte `SqliteTestDatabase`; (2) tampered→verweigert, kein Set; (3) ETag: 1. 200+Cache, 2. If-None-Match→304→Cache; (4) Registry MarkInstalled/IsInstalled Disk-Roundtrip; (5) 213 bestehende grün) → Task 2 Tests (1)+(2) mit `SqliteTestDatabase`; Task 1 Tests (3) + sha256 + unreachable + corrupt-cache; Task 3 Test (4); Task 4 Step 8 verifiziert (5) via voller Suite (224). ✓
- §Live-E2E (Mark: Picker→Demo-Pack sichtbar→Import→„Importiert" nach Neu-Öffnen→Offline nur bundled+Hinweis) → Task 6 (5 Schritte mit Beweisen). ✓
- §Ausführungsreihenfolge (erst App gegen Fixtures voll testbar, dann Repo+Seed+Pages extern, dann E2E) → Tasks 1–4 (Fixtures, kein externer Effekt) → Task 5 (extern, Gate) → Task 6 (E2E). ✓

**Platzhalter-Scan:** Keine „TBD"/„similar to Task N"/„add error handling"-Platzhalter. Jeder Code-Block ist vollständig. „Implementierer-Verify"-Boxen benennen nur die drei versionsabhängigen Umgebungspunkte mit konkretem Fallback: (a) `EntityTagHeaderValue`-ETag-Round-Trip (Task 1), (b) `SHA256.HashData` vs. `Get-FileHash` in Windows PowerShell 5.1 (Task 5 Step 3), (c) exakte `gh api`-Pages-Form inkl. 409→PUT (Task 5 Step 4). Die XAML-Button-Content-Variante (Task 4 Step 4) nennt eine bevorzugte + eine Fallback-Form (zwei TextBlocks vs. Converter) — keine offene Lücke, sondern zwei baubare Wege. Das ungenutzte `using Flippo.App.Services;` im Task-2-Test ist explizit als „vor Grün streichen" markiert (CS8019 unter `TreatWarningsAsErrors`).

**Typ-Konsistenz:**
- `CatalogClient(string baseUrl, string cacheFilePath, HttpMessageHandler? handler = null)` — identisch in Ctor, allen Test-Aufrufen (Task 1 + Task 2) und DI (Task 4 Step 7). ✓
- `GetIndexAsync(CancellationToken) : Task<CatalogIndex?>` und `DownloadPackAsync(CatalogPack, CancellationToken) : Task<ThemeSetFile>` — identisch in Client, Tests, VM (Task 4). ✓
- `CatalogPack` (11 Felder, camelCase) → gelesen im VM (`Id`, `Title`, `SourceLanguage`, `TargetLanguage`, `PackVersion`, `EntryCount`, `SizeBytes`, `Sha256`, `Url`) und geschrieben von `build-index.ps1` (dieselben Feldnamen). ✓
- `ThemeSetImporter.ImportFileAsync(ThemeSetFile, string, long) : Task<ThemeSetImportResult?>` — Signatur identisch in Data-Impl (Task 2), Data-Test (Task 2) und VM-Aufruf (Task 4). `ImportAsync` delegiert an dieselbe Methode. ✓
- `InstalledPacksRegistry(string? filePath = null)` mit `IsInstalled(string):bool`, `MarkInstalled(string,int):void`, `GetAll():IReadOnlyDictionary<string,int>` — identisch in Impl, Test (Task 3) und VM (Task 4, nutzt `IsInstalled`/`MarkInstalled`). ✓
- `ThemeSetItem` hat jetzt zwei Ctoren (bundled: `ThemeSetManifestEntry`; online: `CatalogPack`) mit `Entry?`/`Pack?` nullable — der VM setzt je Pfad genau einen; `Import`-Command dereferenziert `Entry!`/`Pack!` passend zum `IsOnline`-Zweig. ✓
- `IDialogService.ShowThemeSetPickerAsync(ThemeSetImporter, IThemeSetSource, CatalogClient, InstalledPacksRegistry, string)` — Interface + Impl (Task 4 Step 5) + einziger Aufrufer `SetActionsService` (Task 4 Step 6) konsistent erweitert; VM-Ctor nimmt zusätzlich `IDialogService dialogs` (vom DialogService als `this` übergeben). ✓
- `AppSettings.CatalogBaseUrl : string` (Default `""`) — gelesen in DI (Task 4 Step 7); Default-URL-Konstante `https://solutionworxug.github.io/flippo-content/` == Task-5-Basis-URL == Fixture-`baseUrl`-Muster (mit/ohne Trailing-Slash im Ctor normalisiert). ✓
- resx: Alle neuen `Catalog_*`-Keys (`Catalog_OnlineTag`, `Catalog_DownloadTag`, `Catalog_Download`, `Catalog_Unreachable`, `Catalog_ErrorTitle`, `Catalog_ShaMismatch`, `Catalog_DownloadError`) in **beiden** resx-Dateien (Task 4 Step 2); `ThemeSet_*`-Keys existieren bereits (P12). ✓
