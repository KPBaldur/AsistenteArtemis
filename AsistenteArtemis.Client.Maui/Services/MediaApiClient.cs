using System.Net.Http.Json;
using AsistenteArtemis.Client.Maui.Models.Dtos;

namespace AsistenteArtemis.Client.Maui.Services;

public interface IMediaApiClient
{
    Task<List<MediaDto>> GetMediaAsync();
    Task<MediaDto> UploadAsync(FileResult file, string type);
}

public class MediaApiClient(HttpClient http) : IMediaApiClient
{
    public async Task<List<MediaDto>> GetMediaAsync() =>
        await http.GetFromJsonAsync<List<MediaDto>>("Media") ?? [];

    public async Task<MediaDto> UploadAsync(FileResult file, string type)
    {
        using var stream = await file.OpenReadAsync();
        using var content = new MultipartFormDataContent
        {
            { new StreamContent(stream), "file", file.FileName },
            { new StringContent(type), "type" }
        };

        var response = await http.PostAsync("Media/upload", content);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MediaDto>())!;
    }
}
