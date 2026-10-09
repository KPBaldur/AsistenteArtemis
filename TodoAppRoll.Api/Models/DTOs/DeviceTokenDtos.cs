using System.ComponentModel.DataAnnotations;

namespace TodoAppRoll.Api.Models.DTOs
{
    public class DeviceTokenDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }

    public class DeviceTokenCreateDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    // Respuesta de la creación: la única vez que se ve el token en claro.
    public class DeviceTokenCreatedDto
    {
        public DeviceTokenDto Device { get; set; } = new();
        public string Token { get; set; } = string.Empty;
    }
}
