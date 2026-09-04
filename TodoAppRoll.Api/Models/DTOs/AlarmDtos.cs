using System;
using TodoAppRoll.Api.Models.Enums;

namespace TodoAppRoll.Api.Models.DTOs
{
    public class AlarmDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public AlarmType Type { get; set; }
        public int? DurationMinutes { get; set; }
        public DateTime? ScheduleAt { get; set; }
        public bool IsActive { get; set; }
        public Guid? AudioId { get; set; }
        public Guid? ImageId { get; set; }
        public AlarmDaysOfWeek DaysOfWeek { get; set; }
        public TimeOnly? WindowStart { get; set; }
        public TimeOnly? WindowEnd { get; set; }
        public int? SnoozeMinutes { get; set; }
        public int? Volume { get; set; }
        public bool IsInBreak { get; set; }
    }

    public class AlarmCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public AlarmType Type { get; set; }
        public int? DurationMinutes { get; set; }
        public DateTime? ScheduleAt { get; set; }
        public Guid? AudioId { get; set; }
        public Guid? ImageId { get; set; }
        public AlarmDaysOfWeek? DaysOfWeek { get; set; }
        public TimeOnly? WindowStart { get; set; }
        public TimeOnly? WindowEnd { get; set; }
        public int? SnoozeMinutes { get; set; }
        public int? Volume { get; set; }
    }

    public class AlarmUpdateDto
    {
        public string? Name { get; set; }
        public bool? IsActive { get; set; }
        public int? DurationMinutes { get; set; }
        public DateTime? ScheduleAt { get; set; }
        public Guid? AudioId { get; set; }
        public Guid? ImageId { get; set; }
        public AlarmDaysOfWeek? DaysOfWeek { get; set; }
        public TimeOnly? WindowStart { get; set; }
        public TimeOnly? WindowEnd { get; set; }
        // Editar de "días/horario" a "todo el tiempo" necesita poder limpiar la ventana a null explícitamente.
        public bool ClearWindow { get; set; }
        public int? SnoozeMinutes { get; set; }
        // Igual que ClearWindow, pero para desactivar la pausa activa (SnoozeMinutes en null).
        public bool ClearSnoozeMinutes { get; set; }
        public int? Volume { get; set; }
    }
}
