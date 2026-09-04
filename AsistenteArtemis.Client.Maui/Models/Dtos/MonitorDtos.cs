using AsistenteArtemis.Client.Maui.Models.Enums;

namespace AsistenteArtemis.Client.Maui.Models.Dtos;

// Mirrors TodoAppRoll.Api/Models/DTOs/MonitorDtos.cs.

public class MonitorDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public MonitorCheckType Type { get; set; }
    public string Target { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public MonitorStatus LastStatus { get; set; }
    public DateTime? LastCheckedAt { get; set; }
    public int? LastResponseTimeMs { get; set; }
    public string? LastErrorMessage { get; set; }
}

public class MonitorCreateDto
{
    public string Name { get; set; } = string.Empty;
    public MonitorCheckType Type { get; set; }
    public string Target { get; set; } = string.Empty;
}

public class MonitorCheckResultDto
{
    public DateTime CheckedAt { get; set; }
    public MonitorStatus Status { get; set; }
    public int? ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

// Payload of the "MonitorStatusChanged" SignalR event (Hubs/MonitorHub.cs via
// ServerMonitorService.cs) — Status travels as the enum's STRING name here (.ToString() on the
// server), unlike MonitorDto.LastStatus which travels as an int over plain REST JSON.
public class MonitorStatusChangedPayload
{
    public Guid MonitorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CheckedAt { get; set; }
    public int? ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}
