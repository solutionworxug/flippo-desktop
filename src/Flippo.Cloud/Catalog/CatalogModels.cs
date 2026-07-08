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
