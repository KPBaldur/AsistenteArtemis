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

        // Personalización Multimedia
        public Guid? AudioId { get; set; }
        public virtual Media? Audio { get; set; }
        public Guid? ImageId { get; set; }
        public virtual Media? Image { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
