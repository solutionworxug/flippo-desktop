using System.Text.Json;
using Avalonia.Platform;
using Flippo.Core.Content;

namespace Flippo.App.Services;

/// <summary>
/// Themenset-Quelle aus den in die App gebündelten Assets (<c>avares://Flippo.App/Assets/ThemeSets/</c>).
/// 100% offline. Die spätere C2-Online-Quelle wäre eine zweite <see cref="IThemeSetSource"/>-Implementierung.
/// </summary>
public sealed class BundledThemeSetSource : IThemeSetSource
{
    private const string Base = "avares://Flippo.App/Assets/ThemeSets/";

    public Task<ThemeSetManifest?> LoadManifestAsync() => LoadAsync<ThemeSetManifest>(Base + "manifest.json");

    public Task<ThemeSetFile?> LoadFileAsync(string path)
    {
        // Manifest-Pfad "themesets/en/adjektive.json" -> avares-Ressource unter Assets/ThemeSets/
        var rel = path.StartsWith("themesets/", StringComparison.OrdinalIgnoreCase)
            ? path["themesets/".Length..]
            : path;
        return LoadAsync<ThemeSetFile>(Base + rel);
    }

    private static async Task<T?> LoadAsync<T>(string uri) where T : class
    {
        try
        {
            await using var stream = AssetLoader.Open(new Uri(uri));
            return await JsonSerializer.DeserializeAsync<T>(stream);
        }
        catch
        {
            return null;   // fehlende/ungültige Ressource -> Feature bleibt still funktionsfähig
        }
    }
}
