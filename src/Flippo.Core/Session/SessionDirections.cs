using Flippo.Core.Domain;

namespace Flippo.Core.Session;

/// <summary>
/// Würfelt die Abfragerichtung je Karte einmalig beim Session-Start (Port von buildDirections).
/// <c>true</c> = Quelle als Frage (sourceToTarget). Freitext ist am Desktop richtungs-fest.
/// </summary>
public static class SessionDirections
{
    public static IReadOnlyList<bool> Build(int count, LearningDirection direction, LearningMode mode, Random rng)
    {
        // Freitext prüft am Desktop bewusst nur gegen targetText → immer sourceText als Frage.
        var effective = mode == LearningMode.FreeText ? LearningDirection.SourceToTarget : direction;

        return Enumerable.Range(0, count).Select(_ => effective switch
        {
            LearningDirection.SourceToTarget => true,
            LearningDirection.TargetToSource => false,
            _ => rng.Next(2) == 0   // MIXED: 50/50 je Karte
        }).ToList();
    }
}
