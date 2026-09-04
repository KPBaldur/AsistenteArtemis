using System;
using TodoAppRoll.Api.Models.Enums;

namespace TodoAppRoll.Api.Models.Entities
{
    public class Alarm
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public AlarmType Type { get; set; }
        public int? DurationMinutes { get; set; } // Para Pomodoro
        public DateTime? ScheduleAt { get; set; } // Para DateTime
        public bool IsActive { get; set; } = true;

        // Para Pomodoro: restricción opcional de días/horario (null en WindowStart/WindowEnd = suena todo el tiempo)
        public AlarmDaysOfWeek DaysOfWeek { get; set; } = AlarmDaysOfWeek.All;
        public TimeOnly? WindowStart { get; set; }
        public TimeOnly? WindowEnd { get; set; }
        public int? SnoozeMinutes { get; set; } // Minutos que pospone el botón "Pausar" al sonar
        public int? Volume { get; set; } // 0-100, null = volumen máximo

        // Personalización Multimedia
        public Guid? AudioId { get; set; }
        public virtual Media? Audio { get; set; }
        public Guid? ImageId { get; set; }
        public virtual Media? Image { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
