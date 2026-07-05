using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Services;
using Flippo.Core.Domain;

namespace Flippo.App.ViewModels;

/// <summary>Abschluss-Screen einer Session: Trefferquote und optionales Wiederholen der falschen Karten.</summary>
public sealed partial class SessionSummaryViewModel : ViewModelBase
{
    private readonly NavigationService _nav;

    private string _setName = "";
    private LearningMode _mode;
    private List<long> _wrongEntryIds = [];

    [ObservableProperty] private string _title = "Session beendet";
    [ObservableProperty] private int _correctCount;
    [ObservableProperty] private int _wrongCount;
    [ObservableProperty] private string _quoteText = "";
    [ObservableProperty] private bool _hasWrong;
    [ObservableProperty] private bool _nothingAnswered;

    public SessionSummaryViewModel(NavigationService nav) => _nav = nav;

    public void Initialize(string setName, LearningMode mode, int correct, int wrong, List<long> wrongEntryIds)
    {
        _setName = setName;
        _mode = mode;
        _wrongEntryIds = wrongEntryIds;

        CorrectCount = correct;
        WrongCount = wrong;
        var total = correct + wrong;
        var percent = total > 0 ? (int)Math.Round(100.0 * correct / total) : 0;
        QuoteText = total > 0 ? $"{correct} von {total} richtig · {percent}%" : "Keine Karten beantwortet";
        NothingAnswered = total == 0;
        HasWrong = wrong > 0;
    }

    [RelayCommand]
    private void RepeatWrong()
        => _nav.NavigateTo<LearnSessionViewModel>(
            vm => vm.InitializeFromIds(_wrongEntryIds, _setName, _mode), clearStack: true);

    [RelayCommand] private void Done() => _nav.NavigateTo<SetsOverviewViewModel>(clearStack: true);
}
