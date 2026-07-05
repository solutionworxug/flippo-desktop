using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Flippo.App.Services;

/// <summary>Eine vom Nutzer gewählte Lese-Datei: offener Stream + ursprünglicher Dateiname.</summary>
public sealed record PickedFile(Stream Stream, string Name);

/// <summary>Datei-Dialoge ausschließlich über <see cref="IStorageProvider"/> vom TopLevel (Plan 4.4).</summary>
public interface IFilePickerService
{
    Task<Stream?> OpenReadStreamAsync(string title);

    /// <summary>Öffnet eine Datei mit eigenem Typ-Filter und liefert Stream + Dateiname (Datei-Import, P9).</summary>
    Task<PickedFile?> OpenReadFileAsync(string title, string filterName, params string[] patterns);

    Task<Stream?> SaveWriteStreamAsync(string title, string suggestedFileName);
}

public sealed class FilePickerService : IFilePickerService
{
    private static readonly FilePickerFileType JsonType = new("FLIPPO-Backup (*.json)")
    {
        Patterns = new[] { "*.json" }
    };

    private readonly Func<Window?> _owner;

    public FilePickerService(Func<Window?> owner) => _owner = owner;

    public async Task<Stream?> OpenReadStreamAsync(string title)
    {
        var owner = _owner();
        if (owner is null) return null;

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { JsonType }
        });

        var file = files.Count > 0 ? files[0] : null;
        return file is null ? null : await file.OpenReadAsync();
    }

    public async Task<PickedFile?> OpenReadFileAsync(string title, string filterName, params string[] patterns)
    {
        var owner = _owner();
        if (owner is null) return null;

        var type = new FilePickerFileType(filterName) { Patterns = patterns };
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { type }
        });

        var file = files.Count > 0 ? files[0] : null;
        return file is null ? null : new PickedFile(await file.OpenReadAsync(), file.Name);
    }

    public async Task<Stream?> SaveWriteStreamAsync(string title, string suggestedFileName)
    {
        var owner = _owner();
        if (owner is null) return null;

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "json",
            FileTypeChoices = new[] { JsonType }
        });

        return file is null ? null : await file.OpenWriteAsync();
    }
}
