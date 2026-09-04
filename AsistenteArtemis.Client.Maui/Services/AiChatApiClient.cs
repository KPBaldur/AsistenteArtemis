using System.Net.Http.Json;
using AsistenteArtemis.Client.Maui.Models.Dtos;

namespace AsistenteArtemis.Client.Maui.Services;

public interface IAiChatApiClient
{
    Task<List<ChatSessionDto>> GetSessionsAsync();
    Task<ChatSessionDto> CreateSessionAsync(ChatSessionCreateDto request);
    Task<List<AiMessageDto>> GetMessagesAsync(Guid sessionId);
    Task<SendMessageResponseDto> SendMessageAsync(Guid sessionId, SendMessageDto request);
}

// Registered with a longer HttpClient timeout (60s) than the other resources — SendMessage
// calls the Brain synchronously, which calls Gemini, and can take a while. See MauiProgram.cs.
public class AiChatApiClient(HttpClient http) : IAiChatApiClient
{
    public async Task<List<ChatSessionDto>> GetSessionsAsync() =>
        await http.GetFromJsonAsync<List<ChatSessionDto>>("AiChat/sessions") ?? [];

    public async Task<ChatSessionDto> CreateSessionAsync(ChatSessionCreateDto request)
    {
        var response = await http.PostAsJsonAsync("AiChat/sessions", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChatSessionDto>())!;
    }

    public async Task<List<AiMessageDto>> GetMessagesAsync(Guid sessionId) =>
        await http.GetFromJsonAsync<List<AiMessageDto>>($"AiChat/sessions/{sessionId}/messages") ?? [];

    // Can return a real 502 with a JSON { message: "..." } body if the Brain (Python
    // microservice) is unreachable or errors — EnsureSuccessStatusCode's own exception message
    // is just the status code, so read the body first and surface it via ChatBrainException.
    public async Task<SendMessageResponseDto> SendMessageAsync(Guid sessionId, SendMessageDto request)
    {
        var response = await http.PostAsJsonAsync($"AiChat/sessions/{sessionId}/messages", request);
        if (!response.IsSuccessStatusCode)
        {
            string detail;
            try
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
                detail = error?.Message ?? $"Error {(int)response.StatusCode}";
            }
            catch
            {
                detail = $"Error {(int)response.StatusCode}";
            }

            throw new ChatBrainException(detail);
        }

        return (await response.Content.ReadFromJsonAsync<SendMessageResponseDto>())!;
    }

    private class ErrorBody
    {
        public string? Message { get; set; }
    }
}

// Thrown when the API's AiChat endpoint fails to reach or get a valid reply from the Brain
// (Python microservice) — carries the real server-side error message for display in the UI.
public class ChatBrainException(string message) : Exception(message);
