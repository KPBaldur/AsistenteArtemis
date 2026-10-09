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
        public string? ProjectSlug { get; set; }
        public List<SubtaskDto> Subtasks { get; set; } = new List<SubtaskDto>();
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

    // POST api/Task/reminder: tarea + (opcional) alarma de fecha vinculada, en un solo paso.
    // Un "recordatorio" es una tarea con hora (decisión del 2026-10-09).
    public class ReminderCreateDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? RemindAt { get; set; } // UTC; null = tarea sin alarma
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

    // Separado de TaskUpdateDto para poder distinguir "des-vincular" (AlarmId explícito en null)
    // de "no tocar" (campo ausente) — TaskUpdateDto.AlarmId no puede expresar esa diferencia.
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
}
