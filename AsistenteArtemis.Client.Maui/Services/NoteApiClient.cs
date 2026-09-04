using System.Net.Http.Json;
using AsistenteArtemis.Client.Maui.Models.Dtos;

namespace AsistenteArtemis.Client.Maui.Services;

public interface INoteApiClient
{
    Task<List<NoteDto>> GetNotesAsync();
    Task<NoteDto> CreateNoteAsync(NoteCreateDto request);
    Task UpdateNoteAsync(Guid id, NoteUpdateDto request);
    Task DeleteNoteAsync(Guid id);
}

public class NoteApiClient(HttpClient http) : INoteApiClient
{
    public async Task<List<NoteDto>> GetNotesAsync() =>
        await http.GetFromJsonAsync<List<NoteDto>>("Note") ?? [];

    public async Task<NoteDto> CreateNoteAsync(NoteCreateDto request)
    {
        var response = await http.PostAsJsonAsync("Note", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NoteDto>())!;
    }

    // PUT api/Note/{id} returns 204 No Content.
    public async Task UpdateNoteAsync(Guid id, NoteUpdateDto request)
    {
        var response = await http.PutAsJsonAsync($"Note/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteNoteAsync(Guid id)
    {
        var response = await http.DeleteAsync($"Note/{id}");
        response.EnsureSuccessStatusCode();
    }
}
