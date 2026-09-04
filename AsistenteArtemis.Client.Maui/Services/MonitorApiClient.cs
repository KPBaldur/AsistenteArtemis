using System.Net.Http.Json;
using AsistenteArtemis.Client.Maui.Models.Dtos;

namespace AsistenteArtemis.Client.Maui.Services;

public interface IMonitorApiClient
{
    Task<List<MonitorDto>> GetMonitorsAsync();
    Task<MonitorDto> CreateMonitorAsync(MonitorCreateDto request);
    Task DeleteMonitorAsync(Guid id);
    Task<List<MonitorCheckResultDto>> GetHistoryAsync(Guid id, int take = 50);
}

public class MonitorApiClient(HttpClient http) : IMonitorApiClient
{
    public async Task<List<MonitorDto>> GetMonitorsAsync() =>
        await http.GetFromJsonAsync<List<MonitorDto>>("Monitor") ?? [];

    public async Task<MonitorDto> CreateMonitorAsync(MonitorCreateDto request)
    {
        var response = await http.PostAsJsonAsync("Monitor", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MonitorDto>())!;
    }

    public async Task DeleteMonitorAsync(Guid id)
    {
        var response = await http.DeleteAsync($"Monitor/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<MonitorCheckResultDto>> GetHistoryAsync(Guid id, int take = 50) =>
        await http.GetFromJsonAsync<List<MonitorCheckResultDto>>($"Monitor/{id}/history?take={take}") ?? [];
}
