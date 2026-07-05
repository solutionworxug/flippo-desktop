using CommunityToolkit.Mvvm.ComponentModel;
using Flippo.Core.Backup;

namespace Flippo.App.ViewModels;

/// <summary>Preview vor dem Full-Wipe-Import: Zählungen, optionale SRS-Übernahme, Warnungen.</summary>
public sealed partial class ImportPreviewViewModel : ViewModelBase
{
    public int SetCount { get; }
    public int EntryCount { get; }
    public int SessionCount { get; }
    public bool HasSettings { get; }
    public string WarningsText { get; }
    public bool HasWarnings { get; }
    public string Summary { get; }

    [ObservableProperty] private bool _applySettings;

    public ImportPreviewViewModel(BackupParseResult parsed)
    {
        SetCount = parsed.Content.Sets.Count;
        EntryCount = parsed.Content.Entries.Count;
        SessionCount = parsed.Content.Sessions.Count;
        HasSettings = parsed.Content.Settings is not null;
        ApplySettings = HasSettings;

        HasWarnings = parsed.Warnings.Count > 0;
        WarningsText = string.Join(Environment.NewLine, parsed.Warnings);

        Summary = $"{SetCount} Karteien · {EntryCount} Karten · {SessionCount} Sessions";
    }
}
