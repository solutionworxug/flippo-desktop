using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.App.ViewModels;
using Flippo.App.Views;
using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Destinations;
using Flippo.Cloud.Security;
using Flippo.Core.Content;
using Flippo.Data;
using Flippo.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flippo.App;

public partial class App : Application
{
    /// <summary>App-weiter DI-Container.</summary>
    public static IServiceProvider Services { get; private set; } = default!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppPaths.EnsureDirectories();

        var collection = new ServiceCollection();
        ConfigureServices(collection);
        Services = collection.BuildServiceProvider();

        InitializeDatabase();
        var appSettings = Services.GetRequiredService<SettingsService>().Load();
        L.SetLanguage(appSettings.UiLanguage);   // vor dem Laden der Views (Sprache wirkt nach Neustart)
        ThemeService.Apply(appSettings.UiTheme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shell = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = shell };

            // Update-Check nicht-blockierend im Hintergrund anstoßen (fire-and-forget);
            // im Dev-Betrieb ist das ein No-Op.
            _ = shell.CheckForUpdatesAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddFlippoData(AppPaths.ConnectionString);

        // TopLevel-Zugriff für Datei-/Modal-Dialoge (MainWindow ist zur Aufrufzeit gesetzt).
        services.AddSingleton<Func<Window?>>(_ => () =>
            (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IThemeSetSource, BundledThemeSetSource>();
        services.AddSingleton<ThemeSetImporter>();
        services.AddSingleton<IBundledDictionarySource, BundledDictionarySource>();
        services.AddSingleton<DictionaryInstaller>();
        services.AddSingleton<IDestinationConnector, LocalFolderConnector>();
        // WindowsDpapiTokenVault ist [SupportedOSPlatform("windows")] — die App läuft in diesem
        // Slice ausschließlich unter Windows (WinExe/app.manifest/Velopack-Windows-Ziel).
#pragma warning disable CA1416
        services.AddSingleton<ITokenVault>(_ => new WindowsDpapiTokenVault());
#pragma warning restore CA1416
        services.AddSingleton<IDestinationConnector, GoogleDriveConnector>();
        services.AddSingleton<GoogleDriveConnector>();
        services.AddSingleton<DestinationStore>();
        services.AddSingleton<CloudBackupService>();
        services.AddSingleton<SetActionsService>();

        services.AddSingleton<UpdateService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DictionaryListViewModel>();
        services.AddTransient<UserDictionaryDetailViewModel>();
        services.AddTransient<SetsOverviewViewModel>();
        services.AddTransient<StatisticsViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SetDetailViewModel>();
        services.AddTransient<LearnSessionViewModel>();
        services.AddTransient<SessionSummaryViewModel>();
        services.AddTransient<BackupDestinationsViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    private static void InitializeDatabase()
    {
        var factory = Services.GetRequiredService<IDbContextFactory<FlippoDbContext>>();
        using var db = factory.CreateDbContext();
        DatabaseInitializer.Initialize(db);
    }
}
