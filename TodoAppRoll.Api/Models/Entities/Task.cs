using System;
using System.Collections.Generic;
using TodoAppRoll.Api.Models.Enums;
using TaskStatus = TodoAppRoll.Api.Models.Enums.TaskStatus;

namespace TodoAppRoll.Api.Models.Entities
{
    public class Task
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TaskStatus Status { get; set; } = TaskStatus.Pending;
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public int Order { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Relaciones
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Subtask> Subtasks { get; set; } = new List<Subtask>();
        public Guid? AlarmId { get; set; }
        public virtual Alarm? Alarm { get; set; }
    }

    public class Subtask
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsDone { get; set; }
        public int Order { get; set; }

        public virtual Task Task { get; set; } = null!;
    }
}
