using System.Net.Http.Json;
using AsistenteArtemis.Client.Maui.Models.Dtos;

namespace AsistenteArtemis.Client.Maui.Services;

public interface IDeviceTokenApiClient
{
    Task<List<DeviceTokenDto>> GetDeviceTokensAsync();
    Task<DeviceTokenCreatedDto> CreateDeviceTokenAsync(DeviceTokenCreateDto request);
    Task RevokeDeviceTokenAsync(Guid id);
}

public class DeviceTokenApiClient(HttpClient http) : IDeviceTokenApiClient
{
    public async Task<List<DeviceTokenDto>> GetDeviceTokensAsync() =>
        await http.GetFromJsonAsync<List<DeviceTokenDto>>("DeviceToken") ?? [];

    public async Task<DeviceTokenCreatedDto> CreateDeviceTokenAsync(DeviceTokenCreateDto request)
    {
        var response = await http.PostAsJsonAsync("DeviceToken", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeviceTokenCreatedDto>())!;
    }

    public async Task RevokeDeviceTokenAsync(Guid id)
    {
        var response = await http.DeleteAsync($"DeviceToken/{id}");
        response.EnsureSuccessStatusCode();
    }
}
