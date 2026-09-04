namespace AsistenteArtemis.Client.Maui.Models.Enums;

// Mirrors TodoAppRoll.Api/Models/Enums/AppEnums.cs — same names, same declaration order.
// Enums travel as ints over JSON (no JsonStringEnumConverter on the API side), so the
// ordinal order here must match the API exactly.

public enum TaskPriority
{
    Low,
    Medium,
    High
}

public enum TaskStatus
{
    Pending,
    InProgress,
    Completed,
    Archived
}

public enum AlarmType
{
    Pomodoro,
    DateTime
}

[Flags]
public enum AlarmDaysOfWeek
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    All = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday
}

public enum MonitorCheckType
{
    HttpPing,
    PostgresConnection
}

public enum MonitorStatus
{
    Unknown,
    Up,
    Down
}
