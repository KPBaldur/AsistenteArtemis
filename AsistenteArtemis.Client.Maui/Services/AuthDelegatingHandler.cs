using System.Net;
using System.Net.Http.Headers;
using AsistenteArtemis.Client.Maui.Models.Dtos;

namespace AsistenteArtemis.Client.Maui.Services;

// Attaches the bearer token to every request and, on a 401, refreshes it exactly once even
// under concurrent requests (a SemaphoreSlim serializes the refresh; a request that arrives
// while a refresh is already in flight just reuses its result). Mirrors the single-flight
// interceptor pattern in TodoAppRoll.Web/src/api/axios.ts, adapted to HttpClient.
public class AuthDelegatingHandler(TokenStore tokenStore, IAuthApiClient authApiClient, AuthState authState) : DelegatingHandler
{
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        byte[]? bodyBytes = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        var accessToken = await tokenStore.GetAccessTokenAsync();
        var response = await SendWithTokenAsync(request, bodyBytes, accessToken, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            // Another request may have already refreshed while we were waiting on the lock.
            var currentToken = await tokenStore.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(currentToken) && currentToken != accessToken)
            {
                response.Dispose();
                return await SendWithTokenAsync(request, bodyBytes, currentToken, cancellationToken);
            }

            var refreshToken = await tokenStore.GetRefreshTokenAsync();
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                authState.SetLoggedOut();
                return response;
            }

            try
            {
                var refreshed = await authApiClient.RefreshAsync(new RefreshRequest { Token = accessToken, RefreshToken = refreshToken });
                await tokenStore.SaveTokensAsync(refreshed.Token, refreshed.RefreshToken);
                response.Dispose();
                return await SendWithTokenAsync(request, bodyBytes, refreshed.Token, cancellationToken);
            }
            catch
            {
                tokenStore.ClearTokens();
                authState.SetLoggedOut();
                return response;
            }
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    // Builds a fresh HttpRequestMessage from the buffered body — the original request object
    // can't be re-sent once base.SendAsync has consumed it.
    private async Task<HttpResponseMessage> SendWithTokenAsync(
        HttpRequestMessage original, byte[]? bodyBytes, string? token, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        if (bodyBytes is not null)
        {
            clone.Content = new ByteArrayContent(bodyBytes);
            if (original.Content is not null)
            {
                foreach (var header in original.Content.Headers)
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (!string.IsNullOrEmpty(token))
            clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(clone, cancellationToken);
    }
}
