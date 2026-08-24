using System;
using System.Collections.Generic;

namespace TodoAppRoll.Api.Models.Entities
{
    public class AIChatSession
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = "Nueva Conversación";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<AIMessage> Messages { get; set; } = new List<AIMessage>();
        public virtual User User { get; set; } = null!;
    }

    public class AIMessage
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public string Role { get; set; } = string.Empty; // "user" o "assistant"
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual AIChatSession Session { get; set; } = null!;
    }

    public class AISmartSuggestion
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string SuggestionType { get; set; } = string.Empty; // "Rescheduling", "Priority", "Habit"
        public string Content { get; set; } = string.Empty;
        public bool IsApplied { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
    }
}
