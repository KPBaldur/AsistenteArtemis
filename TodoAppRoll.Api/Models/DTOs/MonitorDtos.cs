using System.ComponentModel.DataAnnotations;
using TodoAppRoll.Api.Models.Enums;

namespace TodoAppRoll.Api.Models.DTOs
{
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
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public MonitorCheckType Type { get; set; }

        [Required]
        public string Target { get; set; } = string.Empty;
    }

    public class MonitorCheckResultDto
    {
        public DateTime CheckedAt { get; set; }
        public MonitorStatus Status { get; set; }
        public int? ResponseTimeMs { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
