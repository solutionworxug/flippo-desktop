using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
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
    private readonly MainWindowViewModel _shell;

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
    [ObservableProperty] private bool _languageChanged;   // Sprache geändert → Neustart-Hinweis anzeigen

    public IReadOnlyList<SettingOption> SrsModeOptions { get; } =
        [new(L.T("Settings_SrsModeBox"), "FLASHCARD_BOX"), new(L.T("Settings_SrsModeAdaptive"), "ADAPTIVE")];

    public IReadOnlyList<SettingOption> DirectionOptions { get; } =
    [
        new(L.T("Settings_DirectionSourceToTarget"), "SOURCE_TO_TARGET"),
        new(L.T("Settings_DirectionTargetToSource"), "TARGET_TO_SOURCE"),
        new(L.T("Settings_DirectionMixed"), "MIXED")
    ];

    public IReadOnlyList<SettingOption> ThemeOptions { get; } =
        [new(L.T("Settings_ThemeSystem"), "System"), new(L.T("Settings_ThemeLight"), "Light"), new(L.T("Settings_ThemeDark"), "Dark")];

    public IReadOnlyList<SettingOption> FontSizeOptions { get; } =
        [new(L.T("Settings_FontSizeSmall"), "Small"), new(L.T("Settings_FontSizeMedium"), "Medium"), new(L.T("Settings_FontSizeLarge"), "Large")];

    public IReadOnlyList<SettingOption> LanguageOptions { get; } =
        [new(L.T("Settings_LanguageDe"), "de"), new(L.T("Settings_LanguageEn"), "en")];

    public SettingsViewModel(SettingsService settings, MainWindowViewModel shell)
    {
        _settings = settings;
        _shell = shell;
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
        var languageBefore = current.UiLanguage;
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
        ThemeService.Apply(updated.UiTheme);           // Theme sofort
        _shell.ApplyFontSize(updated.FontSize);        // Schriftgröße sofort
        LanguageChanged = updated.UiLanguage != languageBefore;   // Sprache erst nach Neustart
        BoxIntervalsText = string.Join(", ", updated.BoxIntervals);   // normalisiert zurückschreiben
        IsSaved = true;
    }

    /// <summary>Startet die App neu (für den Sprachwechsel) — neue Instanz starten, aktuelle beenden.</summary>
    [RelayCommand]
    private void Restart()
    {
        var path = Environment.ProcessPath;
        if (path is not null)
            Process.Start(path);
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
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
