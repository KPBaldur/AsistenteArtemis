using System;

namespace TodoAppRoll.Api.Models.DTOs
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public bool NotificationsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UserUpdateDto
    {
        public string? Email { get; set; }
        public string? Theme { get; set; }
        public bool? NotificationsEnabled { get; set; }
    }
}
