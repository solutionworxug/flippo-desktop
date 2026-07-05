using System.Text.Json;
using Flippo.Core.Domain;

namespace Flippo.Data.Services;

/// <summary>Lädt/speichert settings.json (atomar via Temp+Replace) und konvertiert in Domain-SrsSettings.</summary>
public sealed class SettingsService
{
    private readonly string _path;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SettingsService(string? settingsFilePath = null)
        => _path = settingsFilePath ?? AppPaths.SettingsFile;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new AppSettings();   // korrupte/unlesbare Datei → Defaults, kein Crash
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);

        if (File.Exists(_path))
            File.Replace(tmp, _path, null);   // atomar
        else
            File.Move(tmp, _path);
    }

    public static SrsSettings ToSrsSettings(AppSettings s) => new()
    {
        Mode = ParseSrsMode(s.SrsMode),
        BoxIntervals = s.BoxIntervals.ToList(),
        StrictAccents = s.StrictAccents,
        TypoToleranceEnabled = s.TypoToleranceEnabled,
        LeechThreshold = s.LeechThreshold,
        LearningDirection = ParseDirection(s.LearningDirection),
        MaxCardsPerSession = s.MaxCardsPerSession,
        MaxNewCardsPerDay = s.MaxNewCardsPerDay
    };

    /// <summary>Übernimmt SRS-Felder (z.B. aus einem Backup) in bestehende Settings; UI-Felder bleiben.</summary>
    public static AppSettings WithSrs(AppSettings current, SrsSettings srs) => current with
    {
        SrsMode = srs.Mode == SrsMode.FlashcardBox ? "FLASHCARD_BOX" : "ADAPTIVE",
        BoxIntervals = srs.BoxIntervals.ToList(),
        StrictAccents = srs.StrictAccents,
        TypoToleranceEnabled = srs.TypoToleranceEnabled,
        LeechThreshold = srs.LeechThreshold,
        LearningDirection = srs.LearningDirection switch
        {
            LearningDirection.TargetToSource => "TARGET_TO_SOURCE",
            LearningDirection.Mixed => "MIXED",
            _ => "SOURCE_TO_TARGET"
        },
        MaxCardsPerSession = srs.MaxCardsPerSession,
        MaxNewCardsPerDay = srs.MaxNewCardsPerDay
    };

    private static SrsMode ParseSrsMode(string v) => v switch
    {
        "FLASHCARD_BOX" => SrsMode.FlashcardBox,
        "ADAPTIVE" => SrsMode.Adaptive,
        _ => SrsMode.Adaptive
    };

    private static LearningDirection ParseDirection(string v) => v switch
    {
        "SOURCE_TO_TARGET" => LearningDirection.SourceToTarget,
        "TARGET_TO_SOURCE" => LearningDirection.TargetToSource,
        "MIXED" => LearningDirection.Mixed,
        _ => LearningDirection.SourceToTarget
    };
}
