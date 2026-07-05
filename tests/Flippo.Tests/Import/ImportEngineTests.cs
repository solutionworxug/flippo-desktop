using Flippo.Core.Domain;
using Flippo.Core.Import;

namespace Flippo.Tests.Import;

/// <summary>
/// 1:1-Port von ImportEngineTest.kt (data/importer). Kotlin-Backtick-Namen als Kommentar,
/// gleiche Argumentreihenfolge bei <c>Assert.Equal(expected, actual)</c>.
/// nowMs ist für diese Tests irrelevant (createdAt/updatedAt werden nicht geprüft) → fester Wert.
/// </summary>
public class ImportEngineTests
{
    private const long Now = 1_700_000_000_000L;

    private static (IReadOnlyList<VocabularyEntry> Entries, int Skipped) Map(
        IReadOnlyList<IReadOnlyList<string>> rows, long setId, ColumnMapping mapping)
        => ImportEngine.MapToEntries(rows, setId, mapping, Now);

    // ─── ParseDelimited ───────────────────────────────────────────────

    [Fact] // parseDelimited basic CSV
    public void ParseDelimited_BasicCsv()
    {
        var result = ImportEngine.ParseDelimited("hello,world\nfoo,bar");
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "hello", "world" }, result[0]);
        Assert.Equal(new[] { "foo", "bar" }, result[1]);
    }

    [Fact] // parseDelimited TSV
    public void ParseDelimited_Tsv()
    {
        var result = ImportEngine.ParseDelimited("hello\tworld\nfoo\tbar", delimiter: '\t');
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "hello", "world" }, result[0]);
    }

    [Fact] // parseDelimited quoted field containing delimiter
    public void ParseDelimited_QuotedFieldContainingDelimiter()
    {
        var result = ImportEngine.ParseDelimited("\"hello, world\",foo");
        Assert.Single(result);
        Assert.Equal(new[] { "hello, world", "foo" }, result[0]);
    }

    [Fact] // parseDelimited escaped quote inside quoted field
    public void ParseDelimited_EscapedQuoteInsideQuotedField()
    {
        var result = ImportEngine.ParseDelimited("\"say \"\"hi\"\" now\",test");
        Assert.Equal(new[] { "say \"hi\" now", "test" }, result[0]);
    }

    [Fact] // parseDelimited skips blank lines
    public void ParseDelimited_SkipsBlankLines()
    {
        var result = ImportEngine.ParseDelimited("a,b\n\n   \nc,d");
        Assert.Equal(2, result.Count);
    }

    [Fact] // parseDelimited trims whitespace around fields
    public void ParseDelimited_TrimsWhitespaceAroundFields()
    {
        var result = ImportEngine.ParseDelimited("  hello  ,  world  ");
        Assert.Equal(new[] { "hello", "world" }, result[0]);
    }

    [Fact] // parseDelimited empty content returns empty list
    public void ParseDelimited_EmptyContentReturnsEmptyList()
    {
        var result = ImportEngine.ParseDelimited("");
        Assert.Empty(result);
    }

    [Fact] // parseDelimited single column
    public void ParseDelimited_SingleColumn()
    {
        var result = ImportEngine.ParseDelimited("only\none\ncolumn");
        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { "only" }, result[0]);
    }

    // ─── MapToEntries ─────────────────────────────────────────────────

    [Fact] // mapToEntries basic mapping
    public void MapToEntries_BasicMapping()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "cat", "Katze" },
            new[] { "dog", "Hund" }
        };
        var (entries, skipped) = Map(rows, setId: 1L, mapping: new ColumnMapping());
        Assert.Equal(2, entries.Count);
        Assert.Equal(0, skipped);
        Assert.Equal("cat", entries[0].SourceText);
        Assert.Equal("Katze", entries[0].TargetText);
    }

    [Fact] // mapToEntries skips header row with keyword source
    public void MapToEntries_SkipsHeaderRowWithKeywordSource()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "source", "target" },
            new[] { "cat", "Katze" }
        };
        var (entries, _) = Map(rows, setId: 1L, mapping: new ColumnMapping());
        Assert.Single(entries);
        Assert.Equal("cat", entries[0].SourceText);
    }

    [Fact] // mapToEntries skips header row with keyword word
    public void MapToEntries_SkipsHeaderRowWithKeywordWord()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "Word", "Translation" },
            new[] { "cat", "Katze" }
        };
        var (entries, _) = Map(rows, setId: 1L, mapping: new ColumnMapping());
        Assert.Single(entries);
    }

    [Fact] // mapToEntries skips header row german keywords
    public void MapToEntries_SkipsHeaderRowGermanKeywords()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "Vokabel", "Übersetzung" },
            new[] { "Hund", "dog" }
        };
        var (entries, _) = Map(rows, setId: 1L, mapping: new ColumnMapping());
        Assert.Single(entries);
    }

    [Fact] // mapToEntries skips rows with blank source
    public void MapToEntries_SkipsRowsWithBlankSource()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "", "Katze" },
            new[] { "dog", "Hund" }
        };
        var (entries, skipped) = Map(rows, setId: 1L, mapping: new ColumnMapping());
        Assert.Single(entries);
        Assert.Equal(1, skipped);
    }

    [Fact] // mapToEntries skips rows with blank target
    public void MapToEntries_SkipsRowsWithBlankTarget()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "cat", "" },
            new[] { "dog", "Hund" }
        };
        var (entries, skipped) = Map(rows, setId: 1L, mapping: new ColumnMapping());
        Assert.Single(entries);
        Assert.Equal(1, skipped);
    }

    [Fact] // mapToEntries assigns correct setId
    public void MapToEntries_AssignsCorrectSetId()
    {
        var rows = new List<IReadOnlyList<string>> { new[] { "cat", "Katze" } };
        var (entries, _) = Map(rows, setId: 42L, mapping: new ColumnMapping());
        Assert.Equal(42L, entries[0].SetId);
    }

    [Fact] // mapToEntries maps example sentence column
    public void MapToEntries_MapsExampleSentenceColumn()
    {
        var rows = new List<IReadOnlyList<string>> { new[] { "cat", "Katze", "The cat sits." } };
        var mapping = new ColumnMapping { ExampleSentenceColumn = 2 };
        var (entries, _) = Map(rows, setId: 1L, mapping: mapping);
        Assert.Equal("The cat sits.", entries[0].ExampleSentence);
    }

    [Fact] // mapToEntries maps notes column
    public void MapToEntries_MapsNotesColumn()
    {
        var rows = new List<IReadOnlyList<string>> { new[] { "cat", "Katze", "", "my note" } };
        var mapping = new ColumnMapping { NotesColumn = 3 };
        var (entries, _) = Map(rows, setId: 1L, mapping: mapping);
        Assert.Equal("my note", entries[0].Notes);
    }

    [Fact] // mapToEntries maps tags column semicolon-separated
    public void MapToEntries_MapsTagsColumnSemicolonSeparated()
    {
        var rows = new List<IReadOnlyList<string>> { new[] { "cat", "Katze", "", "", "tier;haustier" } };
        var mapping = new ColumnMapping { TagsColumn = 4 };
        var (entries, _) = Map(rows, setId: 1L, mapping: mapping);
        Assert.Equal(new[] { "tier", "haustier" }, entries[0].Tags);
    }

    [Fact] // mapToEntries maps tags column comma-separated
    public void MapToEntries_MapsTagsColumnCommaSeparated()
    {
        var rows = new List<IReadOnlyList<string>> { new[] { "cat", "Katze", "", "", "tier,haustier" } };
        var mapping = new ColumnMapping { TagsColumn = 4 };
        var (entries, _) = Map(rows, setId: 1L, mapping: mapping);
        Assert.Equal(new[] { "tier", "haustier" }, entries[0].Tags);
    }

    [Fact] // mapToEntries empty input returns empty list
    public void MapToEntries_EmptyInputReturnsEmptyList()
    {
        var (entries, skipped) = Map(new List<IReadOnlyList<string>>(), setId: 1L, mapping: new ColumnMapping());
        Assert.Empty(entries);
        Assert.Equal(0, skipped);
    }

    [Fact] // mapToEntries row shorter than mapping uses blank fallback
    public void MapToEntries_RowShorterThanMappingUsesBlankFallback()
    {
        var rows = new List<IReadOnlyList<string>> { new[] { "cat" } };
        var (entries, skipped) = Map(rows, setId: 1L, mapping: new ColumnMapping());
        Assert.Empty(entries);
        Assert.Equal(1, skipped);
    }

    // ─── Desktop-Zusatz: " / "-Alternativen → acceptedAnswers (Plan P9) ──

    [Fact] // Desktop: " / " im Zieltext wird zu targetText + acceptedAnswers gesplittet
    public void MapToEntries_SplitsSlashAlternativesIntoAcceptedAnswers()
    {
        var rows = new List<IReadOnlyList<string>> { new[] { "big", "groß / riesig / mächtig" } };
        var (entries, _) = Map(rows, setId: 1L, mapping: new ColumnMapping());
        Assert.Equal("groß", entries[0].TargetText);
        Assert.Equal(new[] { "riesig", "mächtig" }, entries[0].AcceptedAnswers);
    }

    [Fact] // Desktop: splitAlternatives=false lässt " / " im Zieltext stehen
    public void MapToEntries_KeepsSlashWhenSplitDisabled()
    {
        var rows = new List<IReadOnlyList<string>> { new[] { "big", "groß / riesig" } };
        var mapping = new ColumnMapping { SplitAlternatives = false };
        var (entries, _) = Map(rows, setId: 1L, mapping: mapping);
        Assert.Equal("groß / riesig", entries[0].TargetText);
        Assert.Empty(entries[0].AcceptedAnswers);
    }
}
