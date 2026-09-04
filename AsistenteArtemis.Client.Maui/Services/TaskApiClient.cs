using System.Net.Http.Json;
using AsistenteArtemis.Client.Maui.Models.Dtos;

namespace AsistenteArtemis.Client.Maui.Services;

public interface ITaskApiClient
{
    Task<List<TaskDto>> GetTasksAsync();
    Task<TaskDto> CreateTaskAsync(TaskCreateDto request);
    Task UpdateTaskAsync(Guid id, TaskUpdateDto request);
    Task DeleteTaskAsync(Guid id);
    Task<SubtaskDto> AddSubtaskAsync(Guid taskId, SubtaskCreateDto request);
    Task ToggleSubtaskAsync(Guid subtaskId);
    Task LinkAlarmAsync(Guid taskId, Guid? alarmId);
}

public class TaskApiClient(HttpClient http) : ITaskApiClient
{
    public async Task<List<TaskDto>> GetTasksAsync() =>
        await http.GetFromJsonAsync<List<TaskDto>>("Task") ?? [];

    public async Task<TaskDto> CreateTaskAsync(TaskCreateDto request)
    {
        var response = await http.PostAsJsonAsync("Task", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskDto>())!;
    }

    // PUT api/Task/{id} returns 204 No Content (confirmed against the running API) — no body
    // to parse, unlike POST/GET which return the full TaskDto.
    public async Task UpdateTaskAsync(Guid id, TaskUpdateDto request)
    {
        var response = await http.PutAsJsonAsync($"Task/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTaskAsync(Guid id)
    {
        var response = await http.DeleteAsync($"Task/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<SubtaskDto> AddSubtaskAsync(Guid taskId, SubtaskCreateDto request)
    {
        var response = await http.PostAsJsonAsync($"Task/{taskId}/subtask", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubtaskDto>())!;
    }

    public async Task ToggleSubtaskAsync(Guid subtaskId)
    {
        var response = await http.PatchAsync($"Task/subtask/{subtaskId}/toggle", null);
        response.EnsureSuccessStatusCode();
    }

    // Separado de UpdateTaskAsync: ahí un AlarmId en null es indistinguible de "no tocar".
    public async Task LinkAlarmAsync(Guid taskId, Guid? alarmId)
    {
        var response = await http.PutAsJsonAsync($"Task/{taskId}/alarm", new TaskAlarmUpdateDto { AlarmId = alarmId });
        response.EnsureSuccessStatusCode();
    }
}
