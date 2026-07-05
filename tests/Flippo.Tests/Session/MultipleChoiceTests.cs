using Flippo.Core.Domain;
using Flippo.Core.Session;

namespace Flippo.Tests.Session;

/// <summary>
/// Port von generateOptions (LearnViewModel): korrekte Antwort + 3 Distraktoren aus dem
/// Session-Pool (distinct, ohne aktuelle Karte), bei &lt;3 aus dem Fallback-Pool auffüllen,
/// am Ende geshuffelt. <c>sourceToTarget</c> bestimmt, welche Seite die Antwort ist.
/// </summary>
public class MultipleChoiceTests
{
    private static VocabularyEntry Entry(long id, string source, string target)
        => new() { Id = id, SourceText = source, TargetText = target };

    [Fact] // enough session cards → 4 distinct options including the correct answer
    public void BuildOptions_WithEnoughPool_ReturnsFourDistinctIncludingCorrect()
    {
        var current = Entry(1, "haus", "house");
        var pool = new[]
        {
            current,
            Entry(2, "auto", "car"),
            Entry(3, "baum", "tree"),
            Entry(4, "hund", "dog"),
            Entry(5, "katze", "cat")
        };

        var options = MultipleChoice.BuildOptions(current, sourceToTarget: true, pool, fallbackPool: pool, new Random(0));

        Assert.Contains("house", options);
        Assert.Equal(4, options.Count);
        Assert.Equal(4, options.Distinct().Count());
        Assert.All(options.Where(o => o != "house"),
            o => Assert.Contains(o, new[] { "car", "tree", "dog", "cat" }));
    }

    [Fact] // too few session cards → distractors filled from the fallback pool
    public void BuildOptions_FewSessionCards_FillsFromFallback()
    {
        var current = Entry(1, "haus", "house");
        var session = new[] { current, Entry(2, "auto", "car") };   // nur 1 möglicher Distraktor
        var fallback = new[]
        {
            current,
            Entry(2, "auto", "car"),
            Entry(3, "baum", "tree"),
            Entry(4, "hund", "dog"),
            Entry(5, "katze", "cat")
        };

        var options = MultipleChoice.BuildOptions(current, sourceToTarget: true, session, fallback, new Random(0));

        Assert.Contains("house", options);
        Assert.Equal(4, options.Count);
        Assert.Equal(4, options.Distinct().Count());
    }

    [Fact] // sourceToTarget = false → answers use sourceText
    public void BuildOptions_TargetToSource_UsesSourceText()
    {
        var current = Entry(1, "haus", "house");
        var pool = new[]
        {
            current,
            Entry(2, "auto", "car"),
            Entry(3, "baum", "tree"),
            Entry(4, "hund", "dog")
        };

        var options = MultipleChoice.BuildOptions(current, sourceToTarget: false, pool, pool, new Random(0));

        Assert.Contains("haus", options);
        Assert.All(options.Where(o => o != "haus"),
            o => Assert.Contains(o, new[] { "auto", "baum", "hund" }));
    }

    [Fact] // tiny vocabulary → returns only the correct answer, no crash
    public void BuildOptions_TinyVocabulary_ReturnsWhatIsAvailable()
    {
        var current = Entry(1, "haus", "house");
        var session = new[] { current };

        var options = MultipleChoice.BuildOptions(current, sourceToTarget: true, session, session, new Random(0));

        Assert.Equal(new[] { "house" }, options);
    }
}
