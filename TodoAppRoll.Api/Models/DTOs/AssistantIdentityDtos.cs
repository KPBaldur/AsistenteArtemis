namespace TodoAppRoll.Api.Models.DTOs
{
    public class AssistantIdentityDto
    {
        public string Name { get; set; } = string.Empty;
        public string Tone { get; set; } = string.Empty;
        public string StyleRules { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }

    public class AssistantIdentityUpdateDto
    {
        public string? Name { get; set; }
        public string? Tone { get; set; }
        public string? StyleRules { get; set; }
    }
}
