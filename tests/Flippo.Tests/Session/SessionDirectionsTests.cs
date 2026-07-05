using Flippo.Core.Domain;
using Flippo.Core.Session;

namespace Flippo.Tests.Session;

/// <summary>
/// Port von buildDirections (LearnViewModel). <c>true</c> = Quelle als Frage (sourceToTarget).
/// Desktop-Zusatz: Freitext ist richtungs-fest auf SOURCE_TO_TARGET (Plan 1.4).
/// </summary>
public class SessionDirectionsTests
{
    [Fact] // SOURCE_TO_TARGET → every card asks source→target (true)
    public void Build_SourceToTarget_AllTrue()
    {
        var dirs = SessionDirections.Build(3, LearningDirection.SourceToTarget, LearningMode.Flashcard, new Random(0));

        Assert.Equal(new[] { true, true, true }, dirs);
    }

    [Fact] // TARGET_TO_SOURCE → every card asks target→source (false)
    public void Build_TargetToSource_AllFalse()
    {
        var dirs = SessionDirections.Build(3, LearningDirection.TargetToSource, LearningMode.Flashcard, new Random(0));

        Assert.Equal(new[] { false, false, false }, dirs);
    }

    [Fact] // FreeText overrides any setting → always source→target (Desktop decision, Plan 1.4)
    public void Build_FreeTextMode_ForcesSourceToTarget()
    {
        var dirs = SessionDirections.Build(3, LearningDirection.TargetToSource, LearningMode.FreeText, new Random(0));

        Assert.Equal(new[] { true, true, true }, dirs);
    }

    [Fact] // MIXED → both directions occur across cards/seeds
    public void Build_Mixed_ProducesBothDirections()
    {
        // Über viele Karten + Seeds müssen beide Richtungen auftreten.
        var all = Enumerable.Range(0, 8)
            .SelectMany(seed => SessionDirections.Build(20, LearningDirection.Mixed, LearningMode.Flashcard, new Random(seed)))
            .ToList();

        Assert.Contains(true, all);
        Assert.Contains(false, all);
    }
}
