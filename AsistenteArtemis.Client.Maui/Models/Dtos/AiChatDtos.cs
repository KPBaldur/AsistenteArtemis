namespace AsistenteArtemis.Client.Maui.Models.Dtos;

// Mirrors TodoAppRoll.Api/Models/DTOs/AiChatDtos.cs. Role is a free-text string ("user"/
// "assistant"), not an enum, on the API side.

public class ChatSessionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ChatSessionCreateDto
{
    public string? Title { get; set; }
}

public class AiMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SendMessageDto
{
    public string Content { get; set; } = string.Empty;
}

public class SendMessageResponseDto
{
    public AiMessageDto UserMessage { get; set; } = new();
    public AiMessageDto AssistantMessage { get; set; } = new();
}
