using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Flippo.Cloud.Abstractions;
using Flippo.Data;

namespace Flippo.Cloud.Security;

/// <summary>
/// <see cref="ITokenVault"/> auf Basis von Windows-DPAPI (<see cref="ProtectedData"/>, Scope
/// <see cref="DataProtectionScope.CurrentUser"/>). Ein verschlüsselter Blob je Schlüssel als Datei
/// <c>{TokensDirectory}/{key}.bin</c> (Base64). Nur auf Windows lauffähig — die App läuft in
/// diesem Slice ausschließlich unter Windows (Velopack-Ziel).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDpapiTokenVault : ITokenVault
{
    private readonly string _directory;

    /// <param name="directory">Ablage-Verzeichnis; Standard = <see cref="AppPaths.TokensDirectory"/>.</param>
    public WindowsDpapiTokenVault(string? directory = null)
        => _directory = directory ?? AppPaths.TokensDirectory;

    public void Store(string key, string secret)
    {
        Directory.CreateDirectory(_directory);
        var plain = Encoding.UTF8.GetBytes(secret);
        var cipher = ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllText(PathFor(key), Convert.ToBase64String(cipher));
    }

    public string? Retrieve(string key)
    {
        var path = PathFor(key);
        if (!File.Exists(path)) return null;
        try
        {
            var cipher = Convert.FromBase64String(File.ReadAllText(path));
            var plain = ProtectedData.Unprotect(cipher, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is IOException or FormatException or CryptographicException)
        {
            return null;   // beschädigt/unlesbar → wie „nicht vorhanden" behandeln (neu verbinden nötig)
        }
    }

    public void Delete(string key)
    {
        try { File.Delete(PathFor(key)); }
        catch (IOException) { /* idempotent — best effort */ }
    }

    private string PathFor(string key) => Path.Combine(_directory, key + ".bin");
}
