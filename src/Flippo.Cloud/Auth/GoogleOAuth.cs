using Flippo.Cloud.Abstractions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;

namespace Flippo.Cloud.Auth;

/// <summary>
/// Google-OAuth per Loopback-PKCE für den Desktop-Client (public client → kein vertrauliches Secret
/// beim Token-Tausch). Scope <c>drive.file</c>, <c>access_type=offline</c> + <c>prompt=consent</c>
/// (erzwingt ein Refresh-Token). Token liegt via <see cref="VaultDataStore"/> DPAPI-verschlüsselt.
/// </summary>
public sealed class GoogleOAuth
{
    /// <summary>Öffentliche Desktop-Client-ID (kein Geheimnis — darf committet werden).</summary>
    public const string ClientId = "489948242017-o7jnrs9phnnqpqaa6cbjk15pufvm1kci.apps.googleusercontent.com";

    /// <summary>Bei Desktop-PKCE nicht vertraulich. Falls die Bibliothek ein nicht-leeres Secret
    /// erzwingt (s. Fallback im Plan-Task), wird dieser Wert zur Build-Zeit aus einer gitignored
    /// Datei injiziert und bleibt sonst leer.</summary>
    private const string ClientSecret = "";

    private static readonly string[] Scopes = { DriveService.Scope.DriveFile };

    private readonly ITokenVault _vault;

    public GoogleOAuth(ITokenVault vault) => _vault = vault;

    /// <summary>Startet den interaktiven Flow (System-Browser + Loopback-Listener) und liefert eine
    /// gültige <see cref="UserCredential"/> (auto-refresh). Der Vault-Prefix ist die Destination-Id.</summary>
    public Task<UserCredential> AuthorizeAsync(string vaultKeyPrefix, CancellationToken ct)
    {
        var flow = CreateFlow(vaultKeyPrefix);
        var app = new AuthorizationCodeInstalledApp(flow, new LocalServerCodeReceiver());
        return app.AuthorizeAsync("user", ct);
    }

    /// <summary>Rekonstruiert eine <see cref="UserCredential"/> aus dem gespeicherten Refresh-Token
    /// (kein Browser). <c>null</c>, wenn kein/ungültiges Token vorliegt → Zustand NotConnected.</summary>
    public async Task<UserCredential?> LoadAsync(string vaultKeyPrefix, CancellationToken ct)
    {
        var flow = CreateFlow(vaultKeyPrefix);
        var token = await flow.LoadTokenAsync("user", ct);
        if (token is null || string.IsNullOrEmpty(token.RefreshToken)) return null;
        return new UserCredential(flow, "user", token);
    }

    /// <summary>Widerruft (best effort) und löscht das Token aus dem Vault (beim Entfernen des Ziels).</summary>
    public async Task RevokeAsync(string vaultKeyPrefix, CancellationToken ct)
    {
        var cred = await LoadAsync(vaultKeyPrefix, ct);
        if (cred is not null)
        {
            try { await cred.RevokeTokenAsync(ct); } catch { /* offline/abgelaufen → egal */ }
        }
        await CreateFlow(vaultKeyPrefix).DataStore.DeleteAsync<TokenResponse>("user");
    }

    private GoogleAuthorizationCodeFlow CreateFlow(string vaultKeyPrefix) => new(
        new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = ClientId, ClientSecret = ClientSecret },
            Scopes = Scopes,
            DataStore = new VaultDataStore(_vault, vaultKeyPrefix),
            // Refresh-Token erzwingen (sonst kommt es nur beim allerersten Consent):
            Prompt = "consent"
        });
}
