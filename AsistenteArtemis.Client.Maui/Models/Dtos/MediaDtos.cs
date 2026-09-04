namespace AsistenteArtemis.Client.Maui.Models.Dtos;

// Mirrors TodoAppRoll.Api/Models/DTOs/MediaDtos.cs.

public class MediaDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
