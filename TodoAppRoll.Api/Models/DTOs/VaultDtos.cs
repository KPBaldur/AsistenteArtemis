using System.ComponentModel.DataAnnotations;

namespace TodoAppRoll.Api.Models.DTOs
{
    // Pendiente o comentario para un proyecto del cerebro (POST api/Vault/pendiente|comentario).
    public class VaultNoteDto
    {
        [Required]
        public string Project { get; set; } = string.Empty;

        [Required, MaxLength(1000)]
        public string Text { get; set; } = string.Empty;
    }
}
