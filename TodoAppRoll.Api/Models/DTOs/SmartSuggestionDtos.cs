namespace TodoAppRoll.Api.Models.DTOs
{
    public class SmartSuggestionDto
    {
        public Guid Id { get; set; }
        public string SuggestionType { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public Guid? RelatedEntityId { get; set; }
        public bool IsApplied { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
