namespace AsistenteArtemis.Client.Maui.Services;

// Wraps SecureStorage (not Preferences — these are credentials) for the access/refresh tokens.
// The username is not a credential, so it's kept in Preferences instead, purely to redisplay
// "logged in as X" on app restart without an extra API round-trip.
public class TokenStore
{
    private const string AccessTokenKey = "artemis_access_token";
    private const string RefreshTokenKey = "artemis_refresh_token";
    private const string UsernameKey = "artemis_username";

    public Task<string?> GetAccessTokenAsync() => SecureStorage.Default.GetAsync(AccessTokenKey);

    public Task<string?> GetRefreshTokenAsync() => SecureStorage.Default.GetAsync(RefreshTokenKey);

    public string? GetSavedUsername() => Preferences.Default.Get<string?>(UsernameKey, null);

    public Task SaveSessionAsync(string accessToken, string refreshToken, string username)
    {
        Preferences.Default.Set(UsernameKey, username);
        return Task.WhenAll(
            SecureStorage.Default.SetAsync(AccessTokenKey, accessToken),
            SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken));
    }

    public Task SaveTokensAsync(string accessToken, string refreshToken) =>
        Task.WhenAll(
            SecureStorage.Default.SetAsync(AccessTokenKey, accessToken),
            SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken));

    public void ClearTokens()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        Preferences.Default.Remove(UsernameKey);
    }
}
