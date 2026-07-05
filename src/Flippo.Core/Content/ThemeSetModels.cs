using System.Text.Json.Serialization;

namespace Flippo.Core.Content;

/// <summary>Manifest der gebündelten Themensets (Port von ManifestDto).</summary>
public sealed class ThemeSetManifest
{
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("themesets")] public List<ThemeSetManifestEntry> ThemeSets { get; set; } = new();
}

/// <summary>Ein Manifest-Eintrag: Metadaten eines Themensets (ohne Vokabeln).</summary>
public sealed class ThemeSetManifestEntry
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("language")] public string Language { get; set; } = "";          // Quellsprach-Code, z.B. "EN"
    [JsonPropertyName("targetLanguage")] public string TargetLanguage { get; set; } = "Deutsch";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("entryCount")] public int EntryCount { get; set; }
    [JsonPropertyName("path")] public string Path { get; set; } = "";                    // z.B. "themesets/en/adjektive.json"
    [JsonPropertyName("availableFrom")] public string AvailableFrom { get; set; } = "";  // am Desktop ignoriert (kein Drip)

    /// <summary>Topic-Schlüssel für die Lokalisierung, z.B. "adjektive" aus "en-adjektive".</summary>
    public string Topic
    {
        get
        {
            int dash = Id.IndexOf('-');
            return dash >= 0 && dash < Id.Length - 1 ? Id[(dash + 1)..] : Id;
        }
    }
}

/// <summary>Eine Themenset-Datei mit Vokabeln (Port von ThemeSetFileDto).</summary>
public sealed class ThemeSetFile
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("language")] public string Language { get; set; } = "";
    [JsonPropertyName("sourceLanguage")] public string SourceLanguage { get; set; } = "";
    [JsonPropertyName("targetLanguage")] public string TargetLanguage { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("entries")] public List<ThemeSetFileEntry> Entries { get; set; } = new();
}

public sealed class ThemeSetFileEntry
{
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("target")] public string Target { get; set; } = "";
    [JsonPropertyName("example")] public string Example { get; set; } = "";
    [JsonPropertyName("pos")] public string Pos { get; set; } = "";
    [JsonPropertyName("notes")] public string Notes { get; set; } = "";
    [JsonPropertyName("tags")] public string Tags { get; set; } = "";
}

/// <summary>
/// Quelle für Themensets. Einzige Implementierung heute: gebündelte Assets (App-Schicht, avares://).
/// Die Abstraktion hält den späteren C2-Online-Katalog offen (zweite Implementierung, gleiche Import-Mechanik).
/// </summary>
public interface IThemeSetSource
{
    Task<ThemeSetManifest?> LoadManifestAsync();
    Task<ThemeSetFile?> LoadFileAsync(string path);
}
