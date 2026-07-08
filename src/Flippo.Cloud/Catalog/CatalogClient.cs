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
