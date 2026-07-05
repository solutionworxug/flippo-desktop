using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace Flippo.App.Services;

/// <summary>
/// Nicht-blockierender Update-Check via Velopack über den GitHub-Releases-Feed (Plan P8 / Abschnitt 9).
/// Im Entwicklungs-Betrieb — also wenn die App NICHT über einen Velopack-Installer läuft — passiert
/// bewusst nichts (kein Crash, kein Netzwerk). Sämtliche Fehler werden geschluckt: ein fehlgeschlagener
/// Update-Check darf den App-Start niemals stören.
/// </summary>
public sealed class UpdateService
{
    // Öffentliches GitHub-Repo als Update-Feed. GithubSource lädt die Release-Assets direkt;
    // ein öffentliches Repo braucht dafür kein Token.
    private const string RepoUrl = "https://github.com/solutionworxug/flippo-desktop";

    private readonly UpdateManager? _manager;
    private UpdateInfo? _pending;

    public UpdateService()
    {
        try
        {
            _manager = new UpdateManager(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
        }
        catch
        {
            // z.B. ungültige URL -> Update-Feature still deaktiviert
            _manager = null;
        }
    }

    /// <summary>True, sobald ein Update heruntergeladen und startbereit ist.</summary>
    public bool IsUpdateReady => _pending is not null;

    /// <summary>
    /// Prüft im Hintergrund auf ein neues Release und lädt es ggf. herunter. Gibt <c>true</c> zurück,
    /// wenn danach ein Update installationsbereit ist. Wirft nie.
    /// </summary>
    public async Task<bool> CheckAndDownloadAsync()
    {
        // Nicht über Velopack installiert (Dev-Betrieb) -> nichts tun.
        if (_manager is null || !_manager.IsInstalled)
            return false;

        try
        {
            _pending = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (_pending is null)
                return false;

            await _manager.DownloadUpdatesAsync(_pending).ConfigureAwait(false);
            return true;
        }
        catch
        {
            _pending = null;
            return false;
        }
    }

    /// <summary>Wendet das heruntergeladene Update an und startet die App neu (nutzergetriggert).</summary>
    public void ApplyAndRestart()
    {
        if (_manager is not null && _pending is not null)
            _manager.ApplyUpdatesAndRestart(_pending);
    }
}
