using System;

namespace TodoAppRoll.Api.Models.DTOs
{
    public class MediaDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class MediaUploadDto
    {
        public string Name { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty; // "audio" o "image"
        // En una implementación real, aquí recibiríamos el IFormFile
    }
}
