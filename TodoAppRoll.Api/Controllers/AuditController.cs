using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TodoAppRoll.Api.Data;
using TodoAppRoll.Api.Models.Entities;

namespace TodoAppRoll.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuditController(AppDbContext context)
        {
            _context = context;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AuditLog>>> GetMyLogs()
        {
            var userId = GetUserId();
            var logs = await _context.AuditLogs
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.Timestamp)
                .Take(100)
                .ToListAsync();

            return Ok(logs);
        }

        [HttpPost]
        public async Task<IActionResult> CreateLog(string action, string entity, string payload)
        {
            var userId = GetUserId();
            var log = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = action,
                EntityName = entity,
                PayloadJson = payload,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
