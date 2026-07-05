using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Services;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>
/// Einstellungen (P7): SRS-Verhalten + UI (Theme/Schriftgröße/Sprache). Persistiert über
/// <see cref="SettingsService"/> in settings.json. Theme wirkt sofort, Schriftgröße/Sprache nach Neustart.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settings;

    // SRS
    [ObservableProperty] private SettingOption _srsMode;
    [ObservableProperty] private string _boxIntervalsText = "";
    [ObservableProperty] private bool _strictAccents;
    [ObservableProperty] private bool _typoToleranceEnabled;
    [ObservableProperty] private int _leechThreshold;
    [ObservableProperty] private SettingOption _learningDirection;
    [ObservableProperty] private int _maxCardsPerSession;
    [ObservableProperty] private int _maxNewCardsPerDay;

    // UI
    [ObservableProperty] private SettingOption _uiTheme;
    [ObservableProperty] private SettingOption _fontSize;
    [ObservableProperty] private SettingOption _uiLanguage;

    [ObservableProperty] private bool _isSaved;

    public IReadOnlyList<SettingOption> SrsModeOptions { get; } =
        [new("Karteikasten (Fächer)", "FLASHCARD_BOX"), new("Adaptiv (SM-2)", "ADAPTIVE")];

    public IReadOnlyList<SettingOption> DirectionOptions { get; } =
    [
        new("Quelle → Ziel", "SOURCE_TO_TARGET"),
        new("Ziel → Quelle", "TARGET_TO_SOURCE"),
        new("Gemischt", "MIXED")
    ];

    public IReadOnlyList<SettingOption> ThemeOptions { get; } =
        [new("System", "System"), new("Hell", "Light"), new("Dunkel", "Dark")];

    public IReadOnlyList<SettingOption> FontSizeOptions { get; } =
        [new("Klein", "Small"), new("Mittel", "Medium"), new("Groß", "Large")];

    public IReadOnlyList<SettingOption> LanguageOptions { get; } =
        [new("Deutsch", "de"), new("English", "en")];

    public SettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        var s = settings.Load();

        _srsMode = Match(SrsModeOptions, s.SrsMode);
        _boxIntervalsText = string.Join(", ", s.BoxIntervals);
        _strictAccents = s.StrictAccents;
        _typoToleranceEnabled = s.TypoToleranceEnabled;
        _leechThreshold = s.LeechThreshold;
        _learningDirection = Match(DirectionOptions, s.LearningDirection);
        _maxCardsPerSession = s.MaxCardsPerSession;
        _maxNewCardsPerDay = s.MaxNewCardsPerDay;

        _uiTheme = Match(ThemeOptions, s.UiTheme);
        _fontSize = Match(FontSizeOptions, s.FontSize);
        _uiLanguage = Match(LanguageOptions, s.UiLanguage);
    }

    [RelayCommand]
    private void Save()
    {
        var current = _settings.Load();
        var updated = current with
        {
            SrsMode = SrsMode.Value,
            BoxIntervals = ParseIntervals(BoxIntervalsText, current.BoxIntervals),
            StrictAccents = StrictAccents,
            TypoToleranceEnabled = TypoToleranceEnabled,
            LeechThreshold = Math.Max(1, LeechThreshold),
            LearningDirection = LearningDirection.Value,
            MaxCardsPerSession = Math.Max(0, MaxCardsPerSession),
            MaxNewCardsPerDay = Math.Max(0, MaxNewCardsPerDay),
            UiTheme = UiTheme.Value,
            FontSize = FontSize.Value,
            UiLanguage = UiLanguage.Value
        };

        _settings.Save(updated);
        ThemeService.Apply(updated.UiTheme);   // Theme sofort anwenden
        BoxIntervalsText = string.Join(", ", updated.BoxIntervals);   // normalisiert zurückschreiben
        IsSaved = true;
    }

    partial void OnBoxIntervalsTextChanged(string value) => IsSaved = false;
    partial void OnStrictAccentsChanged(bool value) => IsSaved = false;
    partial void OnTypoToleranceEnabledChanged(bool value) => IsSaved = false;

    /// <summary>CSV → 6 aufsteigende, nicht-negative Intervalle; bei ungültiger Eingabe der bisherige Wert.</summary>
    private static IReadOnlyList<int> ParseIntervals(string text, IReadOnlyList<int> fallback)
    {
        var parts = text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new List<int>();
        foreach (var p in parts)
        {
            if (!int.TryParse(p, out var n) || n < 0) return fallback;
            values.Add(n);
        }
        return values.Count == 6 ? values : fallback;
    }

    private static SettingOption Match(IReadOnlyList<SettingOption> options, string value)
        => options.FirstOrDefault(o => o.Value == value) ?? options[0];
}

/// <summary>Anzeige-Text + gespeicherter Wert für eine ComboBox-Option.</summary>
public sealed record SettingOption(string Display, string Value);
