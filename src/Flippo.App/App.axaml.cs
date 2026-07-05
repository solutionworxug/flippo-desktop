using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Flippo.App.Services;
using Flippo.App.ViewModels;
using Flippo.App.Views;
using Flippo.Data;
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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
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

        services.AddSingleton<NavigationService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<SetsOverviewViewModel>();
        services.AddTransient<SetDetailViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    private static void InitializeDatabase()
    {
        var factory = Services.GetRequiredService<IDbContextFactory<FlippoDbContext>>();
        using var db = factory.CreateDbContext();
        DatabaseInitializer.Initialize(db);
    }
}
