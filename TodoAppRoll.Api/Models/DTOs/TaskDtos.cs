using System;
using System.Collections.Generic;
using TodoAppRoll.Api.Models.Enums;
using TaskStatus = TodoAppRoll.Api.Models.Enums.TaskStatus;

namespace TodoAppRoll.Api.Models.DTOs
{
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
        public List<SubtaskDto> Subtasks { get; set; } = new List<SubtaskDto>();
    }

    public class TaskCreateDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public int Order { get; set; }
        public Guid? AlarmId { get; set; }
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
}
