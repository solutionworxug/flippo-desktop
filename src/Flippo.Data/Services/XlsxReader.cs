using ClosedXML.Excel;

namespace Flippo.Data.Services;

/// <summary>
/// Liest eine XLSX-Datei (erste Tabelle) als Zeilen-Liste — das Excel-Pendant zu
/// <see cref="Flippo.Core.Import.ImportEngine.ParseDelimited"/>. Die Zeilen gehen anschließend
/// durch denselben <c>MapToEntries</c>-Pfad. Nutzt ClosedXML (MIT); der Android-Pure-Parser
/// (XmlPullParser) wird am Desktop bewusst NICHT portiert (Plan P9).
/// </summary>
public static class XlsxReader
{
    /// <summary>
    /// Extrahiert die benutzten Zeilen der ersten Tabelle als getrimmte Zellwerte.
    /// Leere Zeilen werden verworfen (wie beim CSV-Parser). Der Stream muss lesbar sein;
    /// nicht-seekbare Streams vorher in einen <see cref="MemoryStream"/> kopieren.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Read(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet is null) return [];

        var rows = new List<IReadOnlyList<string>>();
        foreach (var row in sheet.RowsUsed())
        {
            int lastCol = row.LastCellUsed()?.Address.ColumnNumber ?? 0;
            var cells = new List<string>(lastCol);
            for (int c = 1; c <= lastCol; c++)
                cells.Add(row.Cell(c).GetString().Trim());

            if (cells.Any(v => !string.IsNullOrWhiteSpace(v)))
                rows.Add(cells);
        }
        return rows;
    }
}
