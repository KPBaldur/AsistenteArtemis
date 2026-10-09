using System;

namespace TodoAppRoll.Api.Models.Entities
{
    // Token de larga duración para clientes que no pueden hacer el login interactivo, como la
    // mascota de escritorio (DesktopMascot). Se guarda solo el hash SHA-256: el valor en claro
    // se devuelve una única vez al crearlo. Se revoca marcando RevokedAt (no se borra, para
    // poder ver qué dispositivos estuvieron conectados).
    public class DeviceToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TokenHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
