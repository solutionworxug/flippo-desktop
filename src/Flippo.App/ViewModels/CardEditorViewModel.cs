using CommunityToolkit.Mvvm.ComponentModel;
using Flippo.Core.Domain;

namespace Flippo.App.ViewModels;

/// <summary>
/// Editier-Zustand einer Karte. Hält die zugrunde liegende Entity, damit beim Speichern der
/// SRS-Zustand (boxLevel, difficulty, Zähler, nextReviewAt …) erhalten bleibt und nur die
/// inhaltlichen Felder überschrieben werden. Listen (acceptedAnswers/tags) als Semikolon-Strings.
/// </summary>
public sealed partial class CardEditorViewModel : ViewModelBase
{
    private readonly VocabularyEntry _backing;

    [ObservableProperty] private string _sourceText = "";
    [ObservableProperty] private string _targetText = "";
    [ObservableProperty] private string _acceptedAnswers = "";
    [ObservableProperty] private string _exampleSentence = "";
    [ObservableProperty] private string _notes = "";
    // "Mehr"-Felder
    [ObservableProperty] private string _partOfSpeech = "";
    [ObservableProperty] private string _gender = "";
    [ObservableProperty] private string _pluralForm = "";
    [ObservableProperty] private string _verbForms = "";
    [ObservableProperty] private string _pronunciation = "";
    [ObservableProperty] private string _tags = "";
    [ObservableProperty] private string _mnemonic = "";

    public bool IsNew => _backing.Id == 0;
    public string HeaderText => IsNew ? "Neue Karte" : "Karte bearbeiten";

    public CardEditorViewModel(VocabularyEntry backing)
    {
        _backing = backing;
        SourceText = backing.SourceText;
        TargetText = backing.TargetText;
        AcceptedAnswers = string.Join("; ", backing.AcceptedAnswers);
        ExampleSentence = backing.ExampleSentence;
        Notes = backing.Notes;
        PartOfSpeech = backing.PartOfSpeech;
        Gender = backing.Gender;
        PluralForm = backing.PluralForm;
        VerbForms = backing.VerbForms;
        Pronunciation = backing.Pronunciation;
        Tags = string.Join("; ", backing.Tags);
        Mnemonic = backing.Mnemonic;
    }

    /// <summary>Quelle + Ziel sind Pflicht.</summary>
    public bool HasContent => !string.IsNullOrWhiteSpace(SourceText) && !string.IsNullOrWhiteSpace(TargetText);

    public VocabularyEntry Build(long nowMs) => _backing with
    {
        SourceText = SourceText.Trim(),
        TargetText = TargetText.Trim(),
        AcceptedAnswers = SplitList(AcceptedAnswers),
        ExampleSentence = ExampleSentence.Trim(),
        Notes = Notes.Trim(),
        PartOfSpeech = PartOfSpeech.Trim(),
        Gender = Gender.Trim(),
        PluralForm = PluralForm.Trim(),
        VerbForms = VerbForms.Trim(),
        Pronunciation = Pronunciation.Trim(),
        Tags = SplitList(Tags),
        Mnemonic = Mnemonic.Trim(),
        CreatedAt = _backing.Id == 0 ? nowMs : _backing.CreatedAt,
        UpdatedAt = nowMs
    };

    private static List<string> SplitList(string value) =>
        value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
