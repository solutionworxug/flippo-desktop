using CommunityToolkit.Mvvm.ComponentModel;
using Flippo.App.Localization;
using Flippo.Core.Domain;
using Flippo.Core.Import;

namespace Flippo.App.ViewModels;

/// <summary>Ergebnis des Datei-Import-Dialogs (null = abgebrochen).</summary>
public sealed record FileImportRequest(long? ExistingSetId, string NewSetName, ColumnMapping Mapping, bool FirstRowIsHeader);

/// <summary>Auswahl-Eintrag für die Spalten-Dropdowns; <see cref="Index"/> <c>-1</c> = „—" (keine Spalte).</summary>
public sealed record ColumnChoice(int Index, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Datei-Import-Dialog (P9): Ziel-Set (neu/bestehend), Spalten-Zuordnung, Kopfzeilen-/Slash-Toggle,
/// Roh-Vorschau der ersten Zeilen. Der eigentliche Import läuft danach in der SetsOverview.
/// </summary>
public sealed partial class FileImportViewModel : ViewModelBase
{
    public string FileName { get; }
    public int RowCount { get; }
    public string FileSummary => string.Format(L.T("FileImport_FileSummary"), FileName, RowCount);

    // ── Ziel-Set ──
    [ObservableProperty] private bool _targetIsNew;
    [ObservableProperty] private string _newSetName;
    public IReadOnlyList<VocabularySet> ExistingSets { get; }
    [ObservableProperty] private VocabularySet? _selectedExistingSet;
    public bool HasExistingSets => ExistingSets.Count > 0;

    // ── Spalten-Zuordnung ──
    public IReadOnlyList<ColumnChoice> RequiredColumns { get; }   // Quelle/Ziel: ohne „—"
    public IReadOnlyList<ColumnChoice> OptionalColumns { get; }   // Beispiel/Notizen/Tags: mit „—"
    [ObservableProperty] private ColumnChoice _sourceColumn;
    [ObservableProperty] private ColumnChoice _targetColumn;
    [ObservableProperty] private ColumnChoice _exampleColumn;
    [ObservableProperty] private ColumnChoice _notesColumn;
    [ObservableProperty] private ColumnChoice _tagsColumn;

    // ── Optionen ──
    [ObservableProperty] private bool _firstRowIsHeader;
    [ObservableProperty] private bool _splitAlternatives = true;

    // ── Vorschau (erste Zeilen roh, alle Spalten) ──
    public IReadOnlyList<string> PreviewRows { get; }

    public FileImportViewModel(string fileName, IReadOnlyList<IReadOnlyList<string>> rows, IReadOnlyList<VocabularySet> existingSets)
    {
        FileName = fileName;
        RowCount = rows.Count;
        ExistingSets = existingSets;

        // Default-Ziel: bestehende Karteien vorhanden → „bestehende"; sonst „neue"
        TargetIsNew = existingSets.Count == 0;
        SelectedExistingSet = existingSets.Count > 0 ? existingSets[0] : null;
        NewSetName = Path.GetFileNameWithoutExtension(fileName);

        int maxCols = Math.Max(2, rows.Count == 0 ? 0 : rows.Max(r => r.Count));
        var cols = Enumerable.Range(0, maxCols)
            .Select(i => new ColumnChoice(i, string.Format(L.T("FileImport_ColumnN"), i + 1)))
            .ToList();
        var none = new ColumnChoice(-1, L.T("FileImport_ColumnNone"));

        RequiredColumns = cols;
        OptionalColumns = new[] { none }.Concat(cols).ToList();

        SourceColumn = cols[0];
        TargetColumn = cols[1];
        ExampleColumn = none;
        NotesColumn = none;
        TagsColumn = none;

        FirstRowIsHeader = rows.Count > 0 && ImportEngine.IsHeaderRow(rows[0]);
        PreviewRows = rows.Take(6).Select(r => string.Join("   │   ", r)).ToList();
    }

    /// <summary>Sammelt die Dialog-Auswahl in eine <see cref="FileImportRequest"/>.</summary>
    public FileImportRequest BuildRequest() => new(
        ExistingSetId: TargetIsNew ? null : SelectedExistingSet?.Id,
        NewSetName: string.IsNullOrWhiteSpace(NewSetName) ? L.T("FileImport_DefaultSetName") : NewSetName.Trim(),
        Mapping: new ColumnMapping
        {
            SourceTextColumn = SourceColumn.Index,
            TargetTextColumn = TargetColumn.Index,
            ExampleSentenceColumn = ExampleColumn.Index,
            NotesColumn = NotesColumn.Index,
            TagsColumn = TagsColumn.Index,
            SplitAlternatives = SplitAlternatives
        },
        FirstRowIsHeader: FirstRowIsHeader);
}
