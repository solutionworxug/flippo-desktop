using System.Text.Json.Serialization;

namespace Flippo.Core.Content;

/// <summary>Eine gebündelte Wörterbuch-Datei (Port von OnlineDictionaryFile).</summary>
public sealed class BundledDictionaryFile
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("sourceLanguage")] public string SourceLanguage { get; set; } = "";
    [JsonPropertyName("targetLanguage")] public string TargetLanguage { get; set; } = "";
    [JsonPropertyName("level")] public string Level { get; set; } = "";
    [JsonPropertyName("entries")] public List<BundledDictionaryFileEntry> Entries { get; set; } = new();
}

public sealed class BundledDictionaryFileEntry
{
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("target")] public string Target { get; set; } = "";
    [JsonPropertyName("pos")] public string Pos { get; set; } = "";
    [JsonPropertyName("gender")] public string Gender { get; set; } = "";
    [JsonPropertyName("example")] public string Example { get; set; } = "";
}

/// <summary>Ein gebündeltes Wörterbuch im Angebot (Port von BundledDictionary).</summary>
public sealed record BundledDictionaryInfo(string SourceLanguage, string TargetLanguage, string AssetPath);

/// <summary>Registry der mitgelieferten Wörterbücher (Port von BundledDictionaries).</summary>
public static class BundledDictionaries
{
    public static readonly IReadOnlyList<BundledDictionaryInfo> All = new[]
    {
        new BundledDictionaryInfo("Englisch", "Deutsch", "dicts/englisch.json"),
        new BundledDictionaryInfo("Französisch", "Deutsch", "dicts/franzoesisch.json"),
        new BundledDictionaryInfo("Spanisch", "Deutsch", "dicts/spanisch.json"),
        new BundledDictionaryInfo("Deutsch", "Englisch", "dicts/german.json"),
        new BundledDictionaryInfo("Spanisch", "Englisch", "dicts/spanisch_englisch.json"),
        new BundledDictionaryInfo("Französisch", "Englisch", "dicts/franzoesisch_englisch.json"),
    };

    /// <summary>Nur Wörterbücher, deren Zielsprache zur App-Sprache passt.</summary>
    public static IReadOnlyList<BundledDictionaryInfo> OfferedFor(string appTargetLanguage)
        => All.Where(d => d.TargetLanguage == appTargetLanguage).ToList();
}

/// <summary>Quelle für gebündelte Wörterbuch-Dateien (App-Schicht lädt aus avares://).</summary>
public interface IBundledDictionarySource
{
    Task<BundledDictionaryFile?> LoadAsync(string assetPath);
}
