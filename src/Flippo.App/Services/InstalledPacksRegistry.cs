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
