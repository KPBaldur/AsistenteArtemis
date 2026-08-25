using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TodoAppRoll.Api.Data;
using TodoAppRoll.Api.Models.DTOs;
using TodoAppRoll.Api.Models.Entities;
using Task = TodoAppRoll.Api.Models.Entities.Task;

namespace TodoAppRoll.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NoteController : ControllerBase
    {
        private static readonly Regex WikiLinkPattern = new(@"\[\[([^\]|]+)(?:\|[^\]]*)?\]\]", RegexOptions.Compiled);

        private readonly AppDbContext _context;

        public NoteController(AppDbContext context)
        {
            _context = context;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
        }

        private async Task<NoteDto> ToDtoAsync(Note note)
        {
            var outgoing = await _context.NoteLinks
                .Where(l => l.SourceNoteId == note.Id)
                .Select(l => new NoteLinkRefDto { NoteId = l.TargetNoteId, Title = l.TargetNote.Title })
                .ToListAsync();

            var incoming = await _context.NoteLinks
                .Where(l => l.TargetNoteId == note.Id)
                .Select(l => new NoteLinkRefDto { NoteId = l.SourceNoteId, Title = l.SourceNote.Title })
                .ToListAsync();

            return new NoteDto
            {
                Id = note.Id,
                Title = note.Title,
                Content = note.Content,
                PropertiesJson = note.PropertiesJson,
                TaskId = note.TaskId,
                CreatedAt = note.CreatedAt,
                UpdatedAt = note.UpdatedAt,
                OutgoingLinks = outgoing,
                IncomingLinks = incoming
            };
        }

        /// <summary>
        /// Re-sincroniza los enlaces salientes de una nota a partir de las referencias [[Título]]
        /// encontradas en su contenido. Solo resuelve enlaces a notas existentes del mismo usuario;
        /// las referencias a títulos inexistentes quedan como texto plano (no rompen nada).
        /// </summary>
        private async System.Threading.Tasks.Task SyncOutgoingLinksAsync(Note note, Guid userId)
        {
            var existingLinks = await _context.NoteLinks
                .Where(l => l.SourceNoteId == note.Id)
                .ToListAsync();
            _context.NoteLinks.RemoveRange(existingLinks);

            var referencedTitles = WikiLinkPattern.Matches(note.Content)
                .Select(m => m.Groups[1].Value.Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (referencedTitles.Count == 0) return;

            var targets = await _context.Notes
                .Where(n => n.UserId == userId && n.Id != note.Id && referencedTitles.Contains(n.Title))
                .ToListAsync();

            foreach (var target in targets)
            {
                _context.NoteLinks.Add(new NoteLink
                {
                    Id = Guid.NewGuid(),
                    SourceNoteId = note.Id,
                    TargetNoteId = target.Id
                });
            }
        }

        // GET: api/Note
        [HttpGet]
        public async Task<ActionResult<IEnumerable<NoteDto>>> GetNotes()
        {
            var userId = GetUserId();
            var notes = await _context.Notes
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.UpdatedAt)
                .ToListAsync();

            var dtos = new List<NoteDto>();
            foreach (var note in notes)
                dtos.Add(await ToDtoAsync(note));

            return Ok(dtos);
        }

        // GET: api/Note/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<NoteDto>> GetNote(Guid id)
        {
            var userId = GetUserId();
            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (note == null) return NotFound();

            return Ok(await ToDtoAsync(note));
        }

        // POST: api/Note
        [HttpPost]
        public async Task<ActionResult<NoteDto>> CreateNote(NoteCreateDto createDto)
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(createDto.Title))
                return BadRequest(new { message = "El título es obligatorio" });

            var note = new Note
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = createDto.Title.Trim(),
                Content = createDto.Content ?? string.Empty,
                PropertiesJson = string.IsNullOrWhiteSpace(createDto.PropertiesJson) ? "{}" : createDto.PropertiesJson,
                TaskId = createDto.TaskId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Notes.Add(note);
            await _context.SaveChangesAsync();

            await SyncOutgoingLinksAsync(note, userId);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetNote), new { id = note.Id }, await ToDtoAsync(note));
        }

        // PUT: api/Note/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateNote(Guid id, NoteUpdateDto updateDto)
        {
            var userId = GetUserId();
            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (note == null) return NotFound();

            if (updateDto.Title != null) note.Title = updateDto.Title.Trim();
            if (updateDto.Content != null) note.Content = updateDto.Content;
            if (updateDto.PropertiesJson != null) note.PropertiesJson = updateDto.PropertiesJson;
            if (updateDto.TaskId.HasValue) note.TaskId = updateDto.TaskId;

            note.UpdatedAt = DateTime.UtcNow;

            if (updateDto.Content != null)
                await SyncOutgoingLinksAsync(note, userId);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Note/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNote(Guid id)
        {
            var userId = GetUserId();
            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (note == null) return NotFound();

            var relatedLinks = await _context.NoteLinks
                .Where(l => l.SourceNoteId == id || l.TargetNoteId == id)
                .ToListAsync();
            _context.NoteLinks.RemoveRange(relatedLinks);

            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
