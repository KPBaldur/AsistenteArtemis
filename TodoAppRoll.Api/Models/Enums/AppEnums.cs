namespace TodoAppRoll.Api.Models.Enums
{
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
}
