using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TodoAppRoll.Api.Data;
using TodoAppRoll.Api.Models.DTOs;
using TodoAppRoll.Api.Models.Entities;

namespace TodoAppRoll.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SmartSuggestionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SmartSuggestionController(AppDbContext context)
        {
            _context = context;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
        }

        private static SmartSuggestionDto ToDto(AISmartSuggestion s) => new SmartSuggestionDto
        {
            Id = s.Id,
            SuggestionType = s.SuggestionType,
            Content = s.Content,
            RelatedEntityId = s.RelatedEntityId,
            IsApplied = s.IsApplied,
            CreatedAt = s.CreatedAt
        };

        // GET: api/SmartSuggestion
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SmartSuggestionDto>>> GetSuggestions()
        {
            var userId = GetUserId();
            var suggestions = await _context.AISmartSuggestions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return Ok(suggestions.Select(ToDto));
        }

        // PATCH: api/SmartSuggestion/{id}
        [HttpPatch("{id}")]
        public async Task<IActionResult> MarkApplied(Guid id)
        {
            var userId = GetUserId();
            var suggestion = await _context.AISmartSuggestions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
            if (suggestion == null) return NotFound();

            suggestion.IsApplied = true;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
