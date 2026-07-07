using System.Text.Json;
using Flippo.Cloud.Abstractions;
using Flippo.Data;

namespace Flippo.App.Services;

/// <summary>
/// Persistiert Backup-Ziel-Configs in destinations.json (nur unsensible Metadaten) und löst
/// eine Config über den passenden Connector in ein Transport-Objekt auf.
/// </summary>
public sealed class DestinationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Dictionary<BackupDestinationKind, IDestinationConnector> _connectors;
    private readonly string _filePath;

    public DestinationStore(IEnumerable<IDestinationConnector> connectors, string? filePath = null)
    {
        _connectors = connectors.ToDictionary(c => c.Kind);
        _filePath = filePath ?? AppPaths.DestinationsFile;
    }

    public IReadOnlyList<DestinationConfig> GetAll()
    {
        if (!File.Exists(_filePath)) return [];
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<DestinationConfig>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];   // beschädigt/unlesbar → als leer behandeln (nicht crashen)
        }
    }

    public void Add(DestinationConfig config)
    {
        var all = GetAll().ToList();
        all.Add(config);
        Persist(all);
    }

    public void Remove(Guid id)
    {
        var all = GetAll().Where(c => c.Id != id).ToList();
        Persist(all);
    }

    public IBackupDestination Resolve(DestinationConfig config)
    {
        if (!_connectors.TryGetValue(config.Kind, out var connector))
            throw new InvalidOperationException($"Kein Connector für {config.Kind} registriert.");
        return connector.Create(config);
    }

    private void Persist(IReadOnlyList<DestinationConfig> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(all, JsonOptions));
    }
}
