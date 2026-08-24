using System;

namespace TodoAppRoll.Api.Models.Entities
{
    public class Media
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string PublicId { get; set; } = string.Empty; // ID en Cloudinary
        public string FileType { get; set; } = string.Empty; // "audio" o "image"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
    }
}
