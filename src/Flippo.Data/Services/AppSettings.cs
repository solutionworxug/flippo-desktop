namespace Flippo.Data.Services;

/// <summary>
/// Persistenz-Modell für settings.json (Plan 4.1). Enums als Strings (Kotlin-Namen), damit
/// die Datei menschenlesbar und driftfest bleibt. Konvertierung → Domain im SettingsService.
/// </summary>
public sealed record AppSettings
{
    public string SrsMode { get; init; } = "ADAPTIVE";
    public IReadOnlyList<int> BoxIntervals { get; init; } = [0, 4, 7, 14, 30, 180];
    public bool StrictAccents { get; init; }
    public bool TypoToleranceEnabled { get; init; } = true;
    public int LeechThreshold { get; init; } = 4;
    public string LearningDirection { get; init; } = "SOURCE_TO_TARGET";
    public int MaxCardsPerSession { get; init; } = 50;
    public int MaxNewCardsPerDay { get; init; }

    // Reine UI-Einstellungen (kein Interop)
    public string UiTheme { get; init; } = "System";    // System | Light | Dark
    public string FontSize { get; init; } = "Medium";   // Small | Medium | Large
    public string UiLanguage { get; init; } = "de";     // de | en
}
