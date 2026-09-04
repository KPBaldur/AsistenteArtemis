using System.Net.Http.Json;
using AsistenteArtemis.Client.Maui.Models.Dtos;

namespace AsistenteArtemis.Client.Maui.Services;

public interface IAlarmApiClient
{
    Task<List<AlarmDto>> GetAlarmsAsync();
    Task<AlarmDto> CreateAlarmAsync(AlarmCreateDto request);
    Task UpdateAlarmAsync(Guid id, AlarmUpdateDto request);
    Task DeleteAlarmAsync(Guid id);
}

public class AlarmApiClient(HttpClient http) : IAlarmApiClient
{
    public async Task<List<AlarmDto>> GetAlarmsAsync() =>
        await http.GetFromJsonAsync<List<AlarmDto>>("Alarm") ?? [];

    public async Task<AlarmDto> CreateAlarmAsync(AlarmCreateDto request)
    {
        var response = await http.PostAsJsonAsync("Alarm", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AlarmDto>())!;
    }

    // PUT api/Alarm/{id} returns 204 No Content — same pattern as Task's PUT.
    public async Task UpdateAlarmAsync(Guid id, AlarmUpdateDto request)
    {
        var response = await http.PutAsJsonAsync($"Alarm/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAlarmAsync(Guid id)
    {
        var response = await http.DeleteAsync($"Alarm/{id}");
        response.EnsureSuccessStatusCode();
    }
}
