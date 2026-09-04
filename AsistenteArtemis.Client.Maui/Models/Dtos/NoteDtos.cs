namespace AsistenteArtemis.Client.Maui.Models.Dtos;

// Mirrors TodoAppRoll.Api/Models/DTOs/NoteDtos.cs. TaskId (link a una tarea) se mantiene en el
// shape pero nunca se setea desde este cliente — sin picker de tareas en esta pasada.

public class NoteDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string PropertiesJson { get; set; } = "{}";
    public Guid? TaskId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<NoteLinkRefDto> OutgoingLinks { get; set; } = new();
    public List<NoteLinkRefDto> IncomingLinks { get; set; } = new();
}

public class NoteLinkRefDto
{
    public Guid NoteId { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class NoteCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? PropertiesJson { get; set; }
    public Guid? TaskId { get; set; }
}

public class NoteUpdateDto
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? PropertiesJson { get; set; }
    public Guid? TaskId { get; set; }
}
