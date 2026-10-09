using AsistenteArtemis.Client.Maui.Models.Enums;
using TaskStatus = AsistenteArtemis.Client.Maui.Models.Enums.TaskStatus;

namespace AsistenteArtemis.Client.Maui.Models.Dtos;

// Mirrors TodoAppRoll.Api/Models/DTOs/TaskDtos.cs.
// The TaskStatus alias above avoids colliding with System.Threading.Tasks.TaskStatus,
// same reason the API itself needs the same alias in TaskDtos.cs/AppDbContext.cs.

public class TaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public int Order { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? AlarmId { get; set; }
    public string? ProjectSlug { get; set; }
    public List<SubtaskDto> Subtasks { get; set; } = new();
}

public class TaskCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public int Order { get; set; }
    public Guid? AlarmId { get; set; }
    public string? ProjectSlug { get; set; }
}

public class TaskUpdateDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public TaskStatus? Status { get; set; }
    public TaskPriority? Priority { get; set; }
    public int? Order { get; set; }
    public Guid? AlarmId { get; set; }
}

public class TaskAlarmUpdateDto
{
    public Guid? AlarmId { get; set; }
}

public class SubtaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public int Order { get; set; }
}

public class SubtaskCreateDto
{
    public string Title { get; set; } = string.Empty;
    public int Order { get; set; }
}
