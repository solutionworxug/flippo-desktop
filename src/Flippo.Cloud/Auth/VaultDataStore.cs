using System.Text.Json;
using Flippo.Cloud.Abstractions;
using Google.Apis.Util.Store;

namespace Flippo.Cloud.Auth;

/// <summary>
/// <see cref="IDataStore"/>-Adapter, der Googles Token-Ablage in den <see cref="ITokenVault"/>
/// (DPAPI) umleitet — das Refresh-Token landet verschlüsselt, nicht in Googles Plaintext-FileDataStore.
/// Ein Vault-Schlüssel je (Prefix, Google-Key): <c>{prefix}_{key}</c>. Unterstrich statt Doppelpunkt,
/// da der Schlüssel 1:1 in einen Dateinamen einfließt — ein Doppelpunkt würde auf NTFS als
/// Alternate-Data-Stream-Trenner interpretiert (verschlüsselter Blob landet in einem versteckten
/// Stream statt einer normalen Datei; Backup-/AV-/Zip-Tools verlieren ihn dann stillschweigend).
/// </summary>
public sealed class VaultDataStore : IDataStore
{
    private readonly ITokenVault _vault;
    private readonly string _prefix;

    public VaultDataStore(ITokenVault vault, string keyPrefix)
    {
        _vault = vault;
        _prefix = keyPrefix;
    }

    public Task StoreAsync<T>(string key, T value)
    {
        _vault.Store(VaultKey(key), JsonSerializer.Serialize(value));
        return Task.CompletedTask;
    }

    public Task<T> GetAsync<T>(string key)
    {
        var json = _vault.Retrieve(VaultKey(key));
        var value = json is null ? default! : JsonSerializer.Deserialize<T>(json)!;
        return Task.FromResult(value);
    }

    public Task DeleteAsync<T>(string key)
    {
        _vault.Delete(VaultKey(key));
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        // Nur der eigene Prefix-Schlüssel ist relevant; ClearAsync wird von unserem Flow nicht genutzt.
        _vault.Delete(VaultKey("user"));
        return Task.CompletedTask;
    }

    private string VaultKey(string key) => $"{_prefix}_{key}";
}
