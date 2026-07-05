using Flippo.Core.Dictionary;
using Flippo.Core.Domain;

namespace Flippo.Tests.Dictionary;

/// <summary>Pure Wörterbuch-Suche: akzent-insensitiv, Ranking (exakt/Präfix/enthält), Limit.</summary>
public class DictionarySearchTests
{
    private static UserDictionaryEntry E(string src, string tgt) => new() { SourceWord = src, TargetWord = tgt };

    private static readonly IReadOnlyList<UserDictionaryEntry> Sample = new[]
    {
        E("hello", "hallo"), E("house", "Haus"), E("café", "Café"), E("world", "Welt"),
    };

    [Fact]
    public void Filter_EmptyQuery_ReturnsAll() => Assert.Equal(4, DictionarySearch.Filter(Sample, "").Count);

    [Fact]
    public void Filter_MatchesTargetWord()
    {
        var r = DictionarySearch.Filter(Sample, "haus");
        Assert.Single(r);
        Assert.Equal("house", r[0].SourceWord);
    }

    [Fact]
    public void Filter_AccentInsensitive()
    {
        var r = DictionarySearch.Filter(Sample, "cafe");   // ohne Akzent trifft "café"
        Assert.Single(r);
        Assert.Equal("café", r[0].SourceWord);
    }

    [Fact]
    public void Filter_RanksExactThenPrefixThenContains()
    {
        var entries = new[] { E("apple", "x"), E("app", "y"), E("pineapple", "z") };
        var r = DictionarySearch.Filter(entries, "app");
        Assert.Equal("app", r[0].SourceWord);
        Assert.Equal("apple", r[1].SourceWord);
        Assert.Equal("pineapple", r[2].SourceWord);
    }

    [Fact]
    public void Filter_RespectsLimit()
    {
        var many = Enumerable.Range(0, 200).Select(i => E($"word{i:000}", "x")).ToArray();
        Assert.Equal(100, DictionarySearch.Filter(many, "word", 100).Count);
    }
}
