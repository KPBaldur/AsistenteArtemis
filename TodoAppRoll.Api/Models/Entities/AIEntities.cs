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
        public string SuggestionType { get; set; } = string.Empty; // "WeeklySummary", "Rescheduling"
        public string Content { get; set; } = string.Empty;
        // Referencia genérica opcional (ej. un Task para "Rescheduling"); null para
        // sugerencias que no son sobre una entidad puntual (ej. "WeeklySummary").
        public Guid? RelatedEntityId { get; set; }
        public bool IsApplied { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
    }

    public class AssistantIdentity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = "Artemis";
        public string Tone { get; set; } = "cercano, directo y con calma";
        public string StyleRules { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
    }
}
