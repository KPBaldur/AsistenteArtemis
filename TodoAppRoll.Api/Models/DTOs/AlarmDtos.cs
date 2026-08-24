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
    }

    public class AlarmCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public AlarmType Type { get; set; }
        public int? DurationMinutes { get; set; }
        public DateTime? ScheduleAt { get; set; }
        public Guid? AudioId { get; set; }
        public Guid? ImageId { get; set; }
    }

    public class AlarmUpdateDto
    {
        public string? Name { get; set; }
        public bool? IsActive { get; set; }
        public int? DurationMinutes { get; set; }
        public DateTime? ScheduleAt { get; set; }
        public Guid? AudioId { get; set; }
        public Guid? ImageId { get; set; }
    }
}
