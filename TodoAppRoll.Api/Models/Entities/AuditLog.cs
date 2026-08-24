using System;

namespace TodoAppRoll.Api.Models.Entities
{
    public class AuditLog
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
    }
}
