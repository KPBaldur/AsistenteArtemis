using System.ComponentModel.DataAnnotations;

namespace TodoAppRoll.Api.Models.DTOs
{
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

    public class AIMessageDto
    {
        public Guid Id { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class SendMessageDto
    {
        [Required]
        public string Content { get; set; } = string.Empty;
    }

    public class SendMessageResponseDto
    {
        public AIMessageDto UserMessage { get; set; } = null!;
        public AIMessageDto AssistantMessage { get; set; } = null!;
    }
}
