using System.Net.Http.Json;
using AsistenteArtemis.Client.Maui.Models.Dtos;

namespace AsistenteArtemis.Client.Maui.Services;

public interface IAuthApiClient
{
    // Register does NOT log the user in — the API returns just the created UserDto, no tokens.
    Task<UserDto> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshAsync(RefreshRequest request);
    Task RevokeAsync(RefreshRequest request);
}

// No AuthDelegatingHandler here on purpose: register/login carry no token yet, and refresh
// sends the expired access token in the request BODY (RefreshRequest.Token), not as a bearer
// header — so this client never needs the "attach current token" behavior.
public class AuthApiClient(HttpClient http) : IAuthApiClient
{
    public async Task<UserDto> RegisterAsync(RegisterRequest request)
    {
        var response = await http.PostAsJsonAsync("Auth/register", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("Auth/login", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request)
    {
        var response = await http.PostAsJsonAsync("Auth/refresh", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public async Task RevokeAsync(RefreshRequest request)
    {
        var response = await http.PostAsJsonAsync("Auth/revoke", request);
        response.EnsureSuccessStatusCode();
    }
}
