using Flippo.Core.Checking;
using Flippo.Core.Domain;

namespace Flippo.Tests.Checking;

/// <summary>Port von FreeTextCheckerTest.kt (39 Tests).</summary>
public class FreeTextCheckerTests
{
    private static VocabularyEntry MakeEntry(string targetText, params string[] acceptedAnswers)
        => new()
        {
            Id = 1L,
            SetId = 1L,
            SourceText = "test",
            TargetText = targetText,
            AcceptedAnswers = acceptedAnswers
        };

    // ---- Exact match ----

    [Fact] // exact match is CORRECT
    public void ExactMatchIsCorrect()
    {
        var entry = MakeEntry("Haus");
        var result = FreeTextChecker.Check("Haus", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.Correct, result.Result);
    }

    [Fact] // case insensitive match is CORRECT
    public void CaseInsensitiveMatchIsCorrect()
    {
        var entry = MakeEntry("Haus");
        var result = FreeTextChecker.Check("haus", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.Correct, result.Result);
    }

    [Fact] // uppercase input matches lowercase target
    public void UppercaseInputMatchesLowercaseTarget()
    {
        var entry = MakeEntry("wasser");
        var result = FreeTextChecker.Check("WASSER", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.Correct, result.Result);
    }

    [Fact] // leading and trailing whitespace is trimmed
    public void LeadingAndTrailingWhitespaceIsTrimmed()
    {
        var entry = MakeEntry("Stadt");
        var result = FreeTextChecker.Check("  Stadt  ", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.Correct, result.Result);
    }

    // ---- Accepted answers ----

    [Fact] // accepted answer is CORRECT
    public void AcceptedAnswerIsCorrect()
    {
        var entry = MakeEntry("sprechen", "reden", "sagen");
        var result = FreeTextChecker.Check("reden", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.Correct, result.Result);
    }

    [Fact] // second accepted answer is also CORRECT
    public void SecondAcceptedAnswerIsAlsoCorrect()
    {
        var entry = MakeEntry("sprechen", "reden", "sagen");
        var result = FreeTextChecker.Check("sagen", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.Correct, result.Result);
    }

    // ---- Accent handling ----

    [Fact] // accent mismatch with tolerance is ALMOST_CORRECT
    public void AccentMismatchWithToleranceIsAlmostCorrect()
    {
        var entry = MakeEntry("sí");
        var result = FreeTextChecker.Check("si", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.AlmostCorrect, result.Result);
    }

    [Fact] // accent mismatch with strict mode is WRONG
    public void AccentMismatchWithStrictModeIsWrong()
    {
        var entry = MakeEntry("sí");
        var result = FreeTextChecker.Check("si", entry, strictAccents: true, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.Wrong, result.Result);
    }

    [Fact] // tú vs tu - accent tolerance
    public void TuAccentTolerance()
    {
        var entry = MakeEntry("tú");
        var result = FreeTextChecker.Check("tu", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.AlmostCorrect, result.Result);
    }

    [Fact] // el vs él - accent tolerance
    public void ElAccentTolerance()
    {
        var entry = MakeEntry("él");
        var result = FreeTextChecker.Check("el", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.AlmostCorrect, result.Result);
    }

    [Fact] // umlaut normalization - uber accepted for über
    public void UmlautNormalizationUberAcceptedForUeber()
    {
        var entry = MakeEntry("über");
        // Nach Normalisierung "über" → "uber"
        var result = FreeTextChecker.Check("uber", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.AlmostCorrect, result.Result);
    }

    // ---- Typo tolerance (Levenshtein) — verschärfte Schwellen ----

    [Fact] // single typo in 9-char word is TYPO
    public void SingleTypoIn9CharWordIsTypo()
    {
        var entry = MakeEntry("verstehen");  // 9 Zeichen
        var result = FreeTextChecker.Check("verstehn", entry, strictAccents: false, typoToleranceEnabled: true);
        Assert.Equal(FreeTextChecker.CheckResult.Typo, result.Result);
    }

    [Fact] // typo disabled - single typo in long word is WRONG
    public void TypoDisabledSingleTypoInLongWordIsWrong()
    {
        var entry = MakeEntry("verstehen");
        var result = FreeTextChecker.Check("verstehn", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.Wrong, result.Result);
    }

    [Fact] // short word up to 8 chars - no typo tolerance (geschaerft)
    public void ShortWordUpTo8CharsNoTypoTolerance()
    {
        var entry = MakeEntry("Hund");
        var result = FreeTextChecker.Check("Hunc", entry, strictAccents: false, typoToleranceEnabled: true);
        Assert.Equal(FreeTextChecker.CheckResult.Wrong, result.Result);
    }

    [Fact] // completely wrong answer is WRONG
    public void CompletelyWrongAnswerIsWrong()
    {
        var entry = MakeEntry("Haus");
        var result = FreeTextChecker.Check("Auto", entry, strictAccents: false, typoToleranceEnabled: true);
        Assert.Equal(FreeTextChecker.CheckResult.Wrong, result.Result);
    }

    // ---- Geschärfte Schwellen — 5-8 Zeichen jetzt WRONG (war TYPO) ----

    [Fact] // 5-char word with 1 typo is now WRONG (geschaerft)
    public void FiveCharWordWith1TypoIsNowWrong()
    {
        var entry = MakeEntry("leben");
        var result = FreeTextChecker.Check("lieb", entry, strictAccents: false, typoToleranceEnabled: true);
        Assert.Equal(FreeTextChecker.CheckResult.Wrong, result.Result);
    }

    [Fact] // 8-char word with 1 typo is now WRONG (geschaerft)
    public void EightCharWordWith1TypoIsNowWrong()
    {
        var entry = MakeEntry("sprechen");
        var result = FreeTextChecker.Check("sprechn", entry, strictAccents: false, typoToleranceEnabled: true);
        Assert.Equal(FreeTextChecker.CheckResult.Wrong, result.Result);
    }

    [Fact] // 13-char word with 2 typos stays TYPO
    public void ThirteenCharWordWith2TyposStaysTypo()
    {
        var entry = MakeEntry("Schmetterling");
        var result = FreeTextChecker.Check("Schmeterlng", entry, strictAccents: false, typoToleranceEnabled: true);
        Assert.Equal(FreeTextChecker.CheckResult.Typo, result.Result);
    }

    [Fact] // german verb haben vs heben - now WRONG (Sinnverschiebung)
    public void HabenVsHebenNowWrong()
    {
        var entry = MakeEntry("haben");
        var result = FreeTextChecker.Check("heben", entry, strictAccents: false, typoToleranceEnabled: true);
        Assert.Equal(FreeTextChecker.CheckResult.Wrong, result.Result);
    }

    [Fact] // english sleep vs sheep - now WRONG (Sinnverschiebung)
    public void SleepVsSheepNowWrong()
    {
        var entry = MakeEntry("sleep");
        var result = FreeTextChecker.Check("sheep", entry, strictAccents: false, typoToleranceEnabled: true);
        Assert.Equal(FreeTextChecker.CheckResult.Wrong, result.Result);
    }

    [Fact] // spanish perro vs pero - now WRONG (Sinnverschiebung)
    public void PerroVsPeroNowWrong()
    {
        var entry = MakeEntry("perro");
        var result = FreeTextChecker.Check("pero", entry, strictAccents: false, typoToleranceEnabled: true);
        Assert.Equal(FreeTextChecker.CheckResult.Wrong, result.Result);
    }

    // ---- Kollisions-Schutz (siblingAnswers) ----

    [Fact] // typo at 9-char word that matches sibling card is WRONG
    public void TypoAt9CharWordThatMatchesSiblingCardIsWrong()
    {
        var entry = MakeEntry("verstehen");  // 9 Zeichen, Schwelle 1
        var result = FreeTextChecker.Check(
            "verstellen",  // Distance 2 — wäre ohne Sibling sowieso WRONG
            entry,
            strictAccents: false,
            typoToleranceEnabled: true,
            siblingAnswers: ["verstellen"]);
        Assert.Equal(FreeTextChecker.CheckResult.Wrong, result.Result);
    }

    [Fact] // typo at 9-char without sibling collision stays TYPO
    public void TypoAt9CharWithoutSiblingCollisionStaysTypo()
    {
        var entry = MakeEntry("verstehen");
        var result = FreeTextChecker.Check(
            "verstehn",
            entry,
            strictAccents: false,
            typoToleranceEnabled: true,
            siblingAnswers: ["sprechen", "lernen"]);
        Assert.Equal(FreeTextChecker.CheckResult.Typo, result.Result);
    }

    [Fact] // empty siblings list works (backward compat)
    public void EmptySiblingsListWorks()
    {
        var entry = MakeEntry("verstehen");
        var result = FreeTextChecker.Check(
            "verstehn",
            entry,
            strictAccents: false,
            typoToleranceEnabled: true);
        Assert.Equal(FreeTextChecker.CheckResult.Typo, result.Result);
    }

    [Fact] // sibling collision via accent-normalized form (non-strict)
    public void SiblingCollisionViaAccentNormalizedForm()
    {
        var entryLong = MakeEntry("verstehen");
        var result = FreeTextChecker.Check(
            "verstehn",
            entryLong,
            strictAccents: false,
            typoToleranceEnabled: true,
            siblingAnswers: ["verstehn"]);  // exakte sibling-Eingabe → Kollision → WRONG
        Assert.Equal(FreeTextChecker.CheckResult.Wrong, result.Result);
    }

    [Fact] // sibling matching current candidate does not block (only OTHER siblings)
    public void SiblingMatchingCurrentCandidateDoesNotBlock()
    {
        var entry = MakeEntry("verstehen");
        var result = FreeTextChecker.Check(
            "verstehn",
            entry,
            strictAccents: false,
            typoToleranceEnabled: true,
            siblingAnswers: ["verstehen"]);  // = current candidate, nicht "anderes"
        Assert.Equal(FreeTextChecker.CheckResult.Typo, result.Result);
    }

    [Fact] // correct answer is returned in outcome
    public void CorrectAnswerIsReturnedInOutcome()
    {
        var entry = MakeEntry("Haus");
        var result = FreeTextChecker.Check("Haus", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal("Haus", result.CorrectAnswer);
    }

    // ---- Normalize function ----

    [Fact] // normalize strips acute accent
    public void NormalizeStripsAcuteAccent()
    {
        Assert.Equal("si", FreeTextChecker.Normalize("sí"));
    }

    [Fact] // normalize strips multiple diacritics
    public void NormalizeStripsMultipleDiacritics()
    {
        Assert.Equal("uber", FreeTextChecker.Normalize("über"));
    }

    [Fact] // normalize handles plain text unchanged
    public void NormalizeHandlesPlainTextUnchanged()
    {
        Assert.Equal("hello", FreeTextChecker.Normalize("hello"));
    }

    // ---- Levenshtein function ----

    [Fact] // levenshtein same strings = 0
    public void LevenshteinSameStrings()
    {
        Assert.Equal(0, FreeTextChecker.Levenshtein("abc", "abc"));
    }

    [Fact] // levenshtein one substitution = 1
    public void LevenshteinOneSubstitution()
    {
        Assert.Equal(1, FreeTextChecker.Levenshtein("abc", "axc"));
    }

    [Fact] // levenshtein one insertion = 1
    public void LevenshteinOneInsertion()
    {
        Assert.Equal(1, FreeTextChecker.Levenshtein("ab", "abc"));
    }

    [Fact] // levenshtein one deletion = 1
    public void LevenshteinOneDeletion()
    {
        Assert.Equal(1, FreeTextChecker.Levenshtein("abc", "ab"));
    }

    [Fact] // levenshtein empty strings
    public void LevenshteinEmptyStrings()
    {
        Assert.Equal(3, FreeTextChecker.Levenshtein("abc", ""));
        Assert.Equal(3, FreeTextChecker.Levenshtein("", "abc"));
        Assert.Equal(0, FreeTextChecker.Levenshtein("", ""));
    }

    // ---- Whitespace-Normalisierung ----

    [Fact] // double space in input is CORRECT
    public void DoubleSpaceInInputIsCorrect()
    {
        var entry = MakeEntry("el gato");
        var result = FreeTextChecker.Check("el  gato", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.Correct, result.Result);
    }

    [Fact] // leading and trailing spaces in input are ignored
    public void LeadingAndTrailingSpacesInInputAreIgnored()
    {
        var entry = MakeEntry("Haus");
        var result = FreeTextChecker.Check("  Haus  ", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.Correct, result.Result);
    }

    [Fact] // double space in candidate is CORRECT
    public void DoubleSpaceInCandidateIsCorrect()
    {
        var entry = MakeEntry("el  gato");
        var result = FreeTextChecker.Check("el gato", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.Correct, result.Result);
    }

    [Fact] // tab in input treated as space
    public void TabInInputTreatedAsSpace()
    {
        var entry = MakeEntry("el gato");
        var result = FreeTextChecker.Check("el\tgato", entry, strictAccents: false, typoToleranceEnabled: false);
        Assert.Equal(FreeTextChecker.CheckResult.Correct, result.Result);
    }
}
