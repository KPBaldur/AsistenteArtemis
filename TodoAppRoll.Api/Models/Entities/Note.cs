using System;
using System.Collections.Generic;

namespace TodoAppRoll.Api.Models.Entities
{
    public class Note
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty; // Cuerpo markdown
        public string PropertiesJson { get; set; } = "{}"; // Frontmatter: tags, tipo, estado, etc.
        public Guid? TaskId { get; set; } // Asociación opcional a una tarea/proyecto existente
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
        public virtual Task? Task { get; set; }
        public virtual ICollection<NoteLink> OutgoingLinks { get; set; } = new List<NoteLink>();
        public virtual ICollection<NoteLink> IncomingLinks { get; set; } = new List<NoteLink>();
    }

    public class NoteLink
    {
        public Guid Id { get; set; }
        public Guid SourceNoteId { get; set; }
        public Guid TargetNoteId { get; set; }

        public virtual Note SourceNote { get; set; } = null!;
        public virtual Note TargetNote { get; set; } = null!;
    }
}
