using Flippo.Core.Util;

namespace Flippo.Tests.Util;

/// <summary>
/// Eigene Tests (kein Kotlin-Pendant): sichern das JVM-Verhalten der JavaCompat-Bausteine ab,
/// insbesondere half-up-Rundung (2.5 → 3, wo .NET banker's 2 liefern würde).
/// </summary>
public class JavaCompatTests
{
    [Theory]
    [InlineData(0.5, 1)]   // .NET Math.Round → 0 (banker's), Java → 1
    [InlineData(1.5, 2)]
    [InlineData(2.5, 3)]   // .NET Math.Round → 2 (banker's), Java → 3
    [InlineData(3.5, 4)]
    [InlineData(0.49, 0)]
    [InlineData(2.4, 2)]
    [InlineData(2.6, 3)]
    [InlineData(130.0, 130)]
    public void RoundHalfUp_MatchesJavaMathRound(double input, long expected)
    {
        Assert.Equal(expected, JavaCompat.RoundHalfUp(input));
    }

    [Fact]
    public void AsciiWhitespace_CollapsesTabAndVerticalTab()
    {
        // Tab (\t), Vertical Tab (\x0B), Formfeed (\f), CR (\r), LF (\n) — alle Java-\s.
        var collapsed = JavaCompat.AsciiWhitespace().Replace("a \t\v\f\r\nb", " ");
        Assert.Equal("a b", collapsed);
    }

    [Fact]
    public void AsciiWhitespace_DoesNotMatchUnicodeNbsp()
    {
        // NBSP (U+00A0) ist Unicode-Whitespace, aber NICHT Java-\s → bleibt erhalten.
        var input = $"a{(char)0x00A0}b";
        Assert.Equal(input, JavaCompat.AsciiWhitespace().Replace(input, " "));
    }

    [Fact]
    public void CombiningDiacriticalMarks_StripsLatinAccentsOnly()
    {
        // Nach NFD wird das Combining Acute (U+0301) entfernt.
        var decomposed = "é".Normalize(System.Text.NormalizationForm.FormD);
        Assert.Equal("e", JavaCompat.CombiningDiacriticalMarks().Replace(decomposed, ""));
    }
}
