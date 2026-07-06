using CommunityToolkit.Mvvm.ComponentModel;
using Flippo.App.Localization;
using Flippo.Core.Domain;

namespace Flippo.App.ViewModels;

/// <summary>Formular für einen Wörterbuch-Eintrag (anlegen/bearbeiten): Wort, Übersetzung, Wortart, Genus, Beispiel.</summary>
public sealed partial class DictEntryEditorViewModel : ViewModelBase
{
    private readonly long _id;
    private readonly long _dictionaryId;
    private readonly IReadOnlyList<string> _acceptedAnswers;
    private readonly string _exampleTranslation;
    private readonly string _level;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _sourceWord = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _targetWord = "";

    [ObservableProperty] private string _partOfSpeech = "";
    [ObservableProperty] private string _gender = "";
    [ObservableProperty] private string _exampleSentence = "";

    public string HeaderText { get; }
    public bool CanSave => !string.IsNullOrWhiteSpace(SourceWord) && !string.IsNullOrWhiteSpace(TargetWord);

    public DictEntryEditorViewModel(long dictionaryId, UserDictionaryEntry? existing)
    {
        _dictionaryId = dictionaryId;
        if (existing is null)
        {
            _acceptedAnswers = [];
            _exampleTranslation = "";
            _level = "";
            HeaderText = L.T("DictEntry_HeaderNew");
        }
        else
        {
            _id = existing.Id;
            _acceptedAnswers = existing.AcceptedAnswers;
            _exampleTranslation = existing.ExampleTranslation;
            _level = existing.Level;
            SourceWord = existing.SourceWord;
            TargetWord = existing.TargetWord;
            PartOfSpeech = existing.PartOfSpeech;
            Gender = existing.Gender;
            ExampleSentence = existing.ExampleSentence;
            HeaderText = L.T("DictEntry_HeaderEdit");
        }
    }

    public UserDictionaryEntry Build() => new()
    {
        Id = _id,
        DictionaryId = _dictionaryId,
        SourceWord = SourceWord.Trim(),
        TargetWord = TargetWord.Trim(),
        PartOfSpeech = PartOfSpeech.Trim(),
        Gender = Gender.Trim(),
        ExampleSentence = ExampleSentence.Trim(),
        ExampleTranslation = _exampleTranslation,
        Level = _level,
        AcceptedAnswers = _acceptedAnswers
    };
}
