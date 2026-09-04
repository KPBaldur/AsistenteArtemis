using AsistenteArtemis.Client.Maui.Models.Enums;

namespace AsistenteArtemis.Client.Maui.Models.Dtos;

// Mirrors TodoAppRoll.Api/Models/DTOs/AlarmDtos.cs. AudioId/ImageId (Media references) are
// kept for shape parity but never set from this client — no media picker in this pass.

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
    public bool ClearWindow { get; set; }
    public int? SnoozeMinutes { get; set; }
    public bool ClearSnoozeMinutes { get; set; }
    public int? Volume { get; set; }
}

// Payload of the "ReceiveAlarmTrigger" SignalR event on /hubs/alarms (Hubs/AlarmHub.cs, fired
// by AlarmSchedulerService.cs every 15s poll when an alarm's ScheduleAt is due, and also by
// AlarmController.TriggerAlarm for manual testing).
public class AlarmTriggeredPayload
{
    public Guid AlarmId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? AudioId { get; set; }
    public Guid? ImageId { get; set; }
    public string? AudioUrl { get; set; }
    public int? Volume { get; set; }
    public bool? HasActiveBreak { get; set; }
    public string? PhaseJustEnded { get; set; }
}
