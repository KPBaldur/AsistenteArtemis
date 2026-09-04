using System.Net.Http.Json;
using AsistenteArtemis.Client.Maui.Models.Dtos;

namespace AsistenteArtemis.Client.Maui.Services;

public interface IAssistantIdentityApiClient
{
    Task<AssistantIdentityDto> GetIdentityAsync();
    Task<AssistantIdentityDto> SaveIdentityAsync(AssistantIdentityUpdateDto request);
}

public class AssistantIdentityApiClient(HttpClient http) : IAssistantIdentityApiClient
{
    public async Task<AssistantIdentityDto> GetIdentityAsync() =>
        (await http.GetFromJsonAsync<AssistantIdentityDto>("AssistantIdentity"))!;

    // Unlike the other resources' PUT, this one is an upsert that returns 200 with the
    // updated AssistantIdentityDto — confirmed reading AssistantIdentityController.UpsertIdentity.
    public async Task<AssistantIdentityDto> SaveIdentityAsync(AssistantIdentityUpdateDto request)
    {
        var response = await http.PutAsJsonAsync("AssistantIdentity", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssistantIdentityDto>())!;
    }
}
