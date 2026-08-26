using System;
using System.Collections.Generic;
using TodoAppRoll.Api.Models.Enums;

namespace TodoAppRoll.Api.Models.Entities
{
    public class ServerMonitor
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public MonitorCheckType Type { get; set; }

        // URL si Type es HttpPing; nombre de una connection string configurada
        // (ej. "DefaultConnection", "NeonMonitoring") si Type es PostgresConnection.
        public string Target { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;

        // Cacheado del último chequeo, para lectura rápida sin ir al historial.
        public MonitorStatus LastStatus { get; set; } = MonitorStatus.Unknown;
        public DateTime? LastCheckedAt { get; set; }
        public int? LastResponseTimeMs { get; set; }
        public string? LastErrorMessage { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual ICollection<MonitorCheckResult> CheckResults { get; set; } = new List<MonitorCheckResult>();
    }

    public class MonitorCheckResult
    {
        public Guid Id { get; set; }
        public Guid MonitorId { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
        public MonitorStatus Status { get; set; }
        public int? ResponseTimeMs { get; set; }
        public string? ErrorMessage { get; set; }

        public virtual ServerMonitor Monitor { get; set; } = null!;
    }
}
