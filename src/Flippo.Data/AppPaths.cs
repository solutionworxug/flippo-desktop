using System.Runtime.InteropServices;

namespace Flippo.Data;

/// <summary>
/// Plattform-korrekte Datenverzeichnisse. macOS/Linux werden explizit gebaut, weil
/// <c>SpecialFolder.ApplicationData</c> auf macOS <c>~/.config</c> liefert (Plan 4.1).
/// </summary>
public static class AppPaths
{
    private const string WindowsAppFolder = "FLIPPO";
    private const string UnixAppFolder = "flippo";

    public static string DataDirectory { get; } = ResolveDataDirectory();
    public static string DatabaseFile => Path.Combine(DataDirectory, "flippo.db");
    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string DestinationsFile => Path.Combine(DataDirectory, "destinations.json");
    public static string BackupsDirectory => Path.Combine(DataDirectory, "backups");
    public static string TokensDirectory => Path.Combine(DataDirectory, "tokens");

    public static string ConnectionString => $"Data Source={DatabaseFile}";

    private static string ResolveDataDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, WindowsAppFolder);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(home, "Library", "Application Support", WindowsAppFolder);
        }

        // Linux: $XDG_DATA_HOME/flippo, Fallback ~/.local/share/flippo
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var dataHome = !string.IsNullOrWhiteSpace(xdg) ? xdg : Path.Combine(home, ".local", "share");
        return Path.Combine(dataHome, UnixAppFolder);
    }

    /// <summary>Legt Daten- und Backups-Verzeichnis an (idempotent).</summary>
    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(TokensDirectory);
    }
}
