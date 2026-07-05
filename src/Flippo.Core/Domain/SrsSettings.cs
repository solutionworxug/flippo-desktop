namespace Flippo.Core.Domain;

/// <summary>
/// Port von SrsSettings.kt. <b>Abweichung vom Kotlin-Klassen-Default:</b> boxIntervals endet auf
/// <c>180</c> statt 45 — das ist der effektive Nutzer-Default am Desktop (Plan 1.1 / 4.1).
/// </summary>
public sealed record SrsSettings
{
    public SrsMode Mode { get; init; } = SrsMode.Adaptive;
    public IReadOnlyList<int> BoxIntervals { get; init; } = [0, 4, 7, 14, 30, 180];
    public bool StrictAccents { get; init; }                    // false
    public bool TypoToleranceEnabled { get; init; } = true;
    public int LeechThreshold { get; init; } = 4;
    public LearningDirection LearningDirection { get; init; } = LearningDirection.SourceToTarget;
    public int MaxCardsPerSession { get; init; } = 50;          // 0 = unbegrenzt
    public int MaxNewCardsPerDay { get; init; }                 // 0 = unbegrenzt
}
