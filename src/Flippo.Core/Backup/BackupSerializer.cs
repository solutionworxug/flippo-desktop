using System.Text.Encodings.Web;
using System.Text.Json;

namespace Flippo.Core.Backup;

/// <summary>Serialisiert/deserialisiert das Backup-JSON. Trennt bewusst Serialisierung ↔ Transport.</summary>
public static class BackupSerializer
{
    // Export: wie Gson — eingerückt, keine Nulls, Umlaute/Akzente nicht als \uXXXX escapen.
    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Import: exakter camelCase-Kontrakt; tolerant gegenüber Kommentaren/Trailing-Commas.
    private static readonly JsonSerializerOptions ImportOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static string Serialize(BackupDataDto data)
        => JsonSerializer.Serialize(data, ExportOptions);

    public static Task SerializeAsync(Stream stream, BackupDataDto data, CancellationToken ct = default)
        => JsonSerializer.SerializeAsync(stream, data, ExportOptions, ct);

    public static BackupDataDto Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<BackupDataDto>(json, ImportOptions)
                   ?? throw new BackupFormatException("Backup ist leer oder null.");
        }
        catch (JsonException ex)
        {
            throw new BackupFormatException($"Ungültiges Backup-Format: {ex.Message}", ex);
        }
    }

    public static async Task<BackupDataDto> DeserializeAsync(Stream stream, CancellationToken ct = default)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<BackupDataDto>(stream, ImportOptions, ct)
                   ?? throw new BackupFormatException("Backup ist leer oder null.");
        }
        catch (JsonException ex)
        {
            throw new BackupFormatException($"Ungültiges Backup-Format: {ex.Message}", ex);
        }
    }
}

public sealed class BackupFormatException : Exception
{
    public BackupFormatException(string message, Exception? inner = null) : base(message, inner) { }
}
