using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace Flippo.Cloud.Destinations;

/// <summary>
/// Verbindet ein Google-Drive-Konto (interaktiv, Browser-OAuth) und baut daraus ein
/// <see cref="GoogleDriveDestination"/>. Der Vault-Schlüssel-Prefix ist die Destination-Id, sodass
/// jedes Ziel sein eigenes Refresh-Token hat.
/// </summary>
public sealed class GoogleDriveConnector : IInteractiveConnector
{
    private readonly ITokenVault _vault;
    private readonly GoogleOAuth _oauth;

    public GoogleDriveConnector(ITokenVault vault)
    {
        _vault = vault;
        _oauth = new GoogleOAuth(vault);
    }

    public BackupDestinationKind Kind => BackupDestinationKind.GoogleDrive;

    public IBackupDestination Create(DestinationConfig config)
    {
        var cred = _oauth.LoadAsync(config.Id.ToString("N"), CancellationToken.None).GetAwaiter().GetResult()
            ?? throw new DestinationException(DestinationState.NotConnected,
                "Kein gültiges Google-Token — bitte neu verbinden.");
        return new GoogleDriveDestination(config, new DriveApi(cred));
    }

    public async Task<DestinationConfig?> ConnectInteractiveAsync(CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var prefix = id.ToString("N");

        UserCredential cred;
        try
        {
            cred = await _oauth.AuthorizeAsync(prefix, ct);
        }
        catch (OperationCanceledException)
        {
            _vault.Delete($"{prefix}_user");   // Teil-Token aufräumen
            return null;                         // Nutzer hat abgebrochen
        }

        var email = await FetchEmailAsync(cred, ct);
        return new DestinationConfig(id, BackupDestinationKind.GoogleDrive,
            $"Google Drive ({email})", new Dictionary<string, string>());
    }

    /// <summary>Konto-E-Mail über <c>about.get(fields=user)</c> — drive.file genügt dafür.</summary>
    private static async Task<string> FetchEmailAsync(UserCredential cred, CancellationToken ct)
    {
        using var service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = cred,
            ApplicationName = "FLIPPO"
        });
        var about = service.About.Get();
        about.Fields = "user(emailAddress)";
        try
        {
            var res = await about.ExecuteAsync(ct);
            return res.User?.EmailAddress ?? "unbekannt";
        }
        catch
        {
            return "unbekannt";   // Anzeigename-Nice-to-have; Verbindung steht trotzdem
        }
    }
}
