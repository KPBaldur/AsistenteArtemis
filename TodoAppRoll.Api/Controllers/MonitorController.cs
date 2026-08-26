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
    public class MonitorController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MonitorController(AppDbContext context)
        {
            _context = context;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
        }

        private static MonitorDto ToDto(ServerMonitor m) => new MonitorDto
        {
            Id = m.Id,
            Name = m.Name,
            Type = m.Type,
            Target = m.Target,
            IsEnabled = m.IsEnabled,
            LastStatus = m.LastStatus,
            LastCheckedAt = m.LastCheckedAt,
            LastResponseTimeMs = m.LastResponseTimeMs,
            LastErrorMessage = m.LastErrorMessage
        };

        // GET: api/Monitor
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MonitorDto>>> GetMonitors()
        {
            var userId = GetUserId();
            var monitors = await _context.Monitors
                .Where(m => m.UserId == userId)
                .ToListAsync();

            return Ok(monitors.Select(ToDto));
        }

        // POST: api/Monitor
        [HttpPost]
        public async Task<ActionResult<MonitorDto>> CreateMonitor(MonitorCreateDto createDto)
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(createDto.Name))
                return BadRequest(new { message = "El nombre es obligatorio" });

            if (string.IsNullOrWhiteSpace(createDto.Target))
                return BadRequest(new { message = "El target es obligatorio" });

            var monitor = new ServerMonitor
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = createDto.Name.Trim(),
                Type = createDto.Type,
                Target = createDto.Target.Trim(),
                IsEnabled = true
            };

            _context.Monitors.Add(monitor);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMonitors), new { id = monitor.Id }, ToDto(monitor));
        }

        // DELETE: api/Monitor/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMonitor(Guid id)
        {
            var userId = GetUserId();
            var monitor = await _context.Monitors.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (monitor == null) return NotFound();

            _context.Monitors.Remove(monitor);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // GET: api/Monitor/{id}/history?take=50
        [HttpGet("{id}/history")]
        public async Task<ActionResult<IEnumerable<MonitorCheckResultDto>>> GetHistory(Guid id, [FromQuery] int take = 50)
        {
            var userId = GetUserId();
            var monitor = await _context.Monitors.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (monitor == null) return NotFound();

            var history = await _context.MonitorCheckResults
                .Where(r => r.MonitorId == id)
                .OrderByDescending(r => r.CheckedAt)
                .Take(Math.Clamp(take, 1, 500))
                .Select(r => new MonitorCheckResultDto
                {
                    CheckedAt = r.CheckedAt,
                    Status = r.Status,
                    ResponseTimeMs = r.ResponseTimeMs,
                    ErrorMessage = r.ErrorMessage
                })
                .ToListAsync();

            return Ok(history);
        }
    }
}
