using System.Text.Json;
using Avalonia.Platform;
using Flippo.Core.Content;

namespace Flippo.App.Services;

/// <summary>Gebündelte Wörterbücher aus den App-Assets (<c>avares://Flippo.App/Assets/Dicts/</c>).</summary>
public sealed class BundledDictionarySource : IBundledDictionarySource
{
    private const string Base = "avares://Flippo.App/Assets/Dicts/";

    public async Task<BundledDictionaryFile?> LoadAsync(string assetPath)
    {
        var rel = assetPath.StartsWith("dicts/", StringComparison.OrdinalIgnoreCase)
            ? assetPath["dicts/".Length..]
            : assetPath;
        try
        {
            await using var stream = AssetLoader.Open(new Uri(Base + rel));
            return await JsonSerializer.DeserializeAsync<BundledDictionaryFile>(stream);
        }
        catch
        {
            return null;
        }
    }
}
