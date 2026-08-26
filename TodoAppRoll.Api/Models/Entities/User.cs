using System;
using System.Collections.Generic;

namespace TodoAppRoll.Api.Models.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Preferencias de Usuario (V2)
        public string Theme { get; set; } = "System"; // Light, Dark, System
        public bool NotificationsEnabled { get; set; } = true;

        // Refresh token (rotado en cada uso)
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        // Relaciones
        public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
        public virtual ICollection<Alarm> Alarms { get; set; } = new List<Alarm>();
        public virtual ICollection<Media> MediaFiles { get; set; } = new List<Media>();
        public virtual ICollection<AIChatSession> ChatSessions { get; set; } = new List<AIChatSession>();
    }
}
