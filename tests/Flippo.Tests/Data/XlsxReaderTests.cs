using ClosedXML.Excel;
using Flippo.Core.Import;
using Flippo.Data.Services;

namespace Flippo.Tests.Data;

/// <summary>
/// XlsxReader (ClosedXML): XLSX in-memory schreiben → lesen → Zeilen/Zellen prüfen, plus Integration
/// mit ImportEngine.MapToEntries. Ersetzt Androids XlsxRoundtripTest (dort der Pure-Parser) —
/// Desktop nutzt ClosedXML, gemeinsame Mapping-Logik.
/// </summary>
public class XlsxReaderTests
{
    private static MemoryStream WriteXlsx(Action<IXLWorksheet> fill)
    {
        var ms = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Sheet1");
            fill(ws);
            wb.SaveAs(ms);
        }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Read_RoundtripsRowsAndCells()
    {
        using var ms = WriteXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "cat";
            ws.Cell(1, 2).Value = "Katze";
            ws.Cell(2, 1).Value = "dog";
            ws.Cell(2, 2).Value = "Hund";
        });

        var rows = XlsxReader.Read(ms);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "cat", "Katze" }, rows[0]);
        Assert.Equal(new[] { "dog", "Hund" }, rows[1]);
    }

    [Fact]
    public void Read_TrimsCellsAndSkipsBlankRows()
    {
        using var ms = WriteXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "  cat  ";
            ws.Cell(1, 2).Value = "Katze";
            // Zeile 2 bleibt leer (nicht benutzt)
            ws.Cell(3, 1).Value = "dog";
            ws.Cell(3, 2).Value = "Hund";
        });

        var rows = XlsxReader.Read(ms);

        Assert.Equal(2, rows.Count);        // leere Zeile verworfen
        Assert.Equal("cat", rows[0][0]);    // getrimmt
    }

    [Fact]
    public void Read_ThenMapToEntries_ProducesCards()
    {
        using var ms = WriteXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Word";           // Header → auto erkannt
            ws.Cell(1, 2).Value = "Translation";
            ws.Cell(2, 1).Value = "big";
            ws.Cell(2, 2).Value = "groß / riesig";
        });

        var rows = XlsxReader.Read(ms);
        var (entries, skipped) = ImportEngine.MapToEntries(rows, setId: 1L, new ColumnMapping(), nowMs: 0L);

        Assert.Single(entries);                     // Kopfzeile übersprungen
        Assert.Equal(0, skipped);
        Assert.Equal("groß", entries[0].TargetText);
        Assert.Equal(new[] { "riesig" }, entries[0].AcceptedAnswers);
    }
}
