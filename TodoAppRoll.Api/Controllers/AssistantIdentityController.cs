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
    public class AssistantIdentityController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AssistantIdentityController(AppDbContext context)
        {
            _context = context;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
        }

        private static AssistantIdentityDto ToDto(AssistantIdentity identity) => new AssistantIdentityDto
        {
            Name = identity.Name,
            Tone = identity.Tone,
            StyleRules = identity.StyleRules,
            UpdatedAt = identity.UpdatedAt
        };

        // GET: api/AssistantIdentity
        [HttpGet]
        public async Task<ActionResult<AssistantIdentityDto>> GetIdentity()
        {
            var userId = GetUserId();
            var identity = await _context.AssistantIdentities.FirstOrDefaultAsync(a => a.UserId == userId);

            // Sin fila todavía: se devuelven los defaults sin crearla, no hace falta seed.
            return Ok(identity != null ? ToDto(identity) : ToDto(new AssistantIdentity()));
        }

        // PUT: api/AssistantIdentity
        [HttpPut]
        public async Task<ActionResult<AssistantIdentityDto>> UpsertIdentity(AssistantIdentityUpdateDto updateDto)
        {
            var userId = GetUserId();
            var identity = await _context.AssistantIdentities.FirstOrDefaultAsync(a => a.UserId == userId);

            if (identity == null)
            {
                identity = new AssistantIdentity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId
                };
                _context.AssistantIdentities.Add(identity);
            }

            if (updateDto.Name != null) identity.Name = updateDto.Name.Trim();
            if (updateDto.Tone != null) identity.Tone = updateDto.Tone.Trim();
            if (updateDto.StyleRules != null) identity.StyleRules = updateDto.StyleRules.Trim();
            identity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(ToDto(identity));
        }
    }
}
