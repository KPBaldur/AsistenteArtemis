using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TodoAppRoll.Api.Data;
using TodoAppRoll.Api.Hubs;
using TodoAppRoll.Api.Models.DTOs;
using TodoAppRoll.Api.Models.Entities;
using TodoAppRoll.Api.Models.Enums;

namespace TodoAppRoll.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AlarmController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<AlarmHub> _hubContext;

        public AlarmController(AppDbContext context, IHubContext<AlarmHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
        }

        // GET: api/Alarm
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AlarmDto>>> GetAlarms()
        {
            var userId = GetUserId();
            var alarms = await _context.Alarms
                .Where(a => a.UserId == userId)
                .Select(a => new AlarmDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Type = a.Type,
                    DurationMinutes = a.DurationMinutes,
                    ScheduleAt = a.ScheduleAt,
                    IsActive = a.IsActive,
                    AudioId = a.AudioId,
                    ImageId = a.ImageId
                })
                .ToListAsync();

            return Ok(alarms);
        }

        // POST: api/Alarm
        [HttpPost]
        public async Task<ActionResult<AlarmDto>> CreateAlarm(AlarmCreateDto createDto)
        {
            var userId = GetUserId();
            var alarm = new Alarm
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = createDto.Name,
                Type = createDto.Type,
                DurationMinutes = createDto.DurationMinutes,
                ScheduleAt = createDto.ScheduleAt,
                AudioId = createDto.AudioId,
                ImageId = createDto.ImageId,
                IsActive = true
            };

            _context.Alarms.Add(alarm);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAlarms), new { id = alarm.Id }, new AlarmDto
            {
                Id = alarm.Id,
                Name = alarm.Name,
                Type = alarm.Type,
                DurationMinutes = alarm.DurationMinutes,
                ScheduleAt = alarm.ScheduleAt,
                IsActive = alarm.IsActive,
                AudioId = alarm.AudioId,
                ImageId = alarm.ImageId
            });
        }

        // PUT: api/Alarm/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAlarm(Guid id, AlarmUpdateDto updateDto)
        {
            var userId = GetUserId();
            var alarm = await _context.Alarms.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (alarm == null) return NotFound();

            if (updateDto.Name != null) alarm.Name = updateDto.Name;
            if (updateDto.IsActive.HasValue) alarm.IsActive = updateDto.IsActive.Value;
            if (updateDto.DurationMinutes.HasValue) alarm.DurationMinutes = updateDto.DurationMinutes;
            if (updateDto.ScheduleAt.HasValue) alarm.ScheduleAt = updateDto.ScheduleAt;
            if (updateDto.AudioId.HasValue) alarm.AudioId = updateDto.AudioId;
            if (updateDto.ImageId.HasValue) alarm.ImageId = updateDto.ImageId;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Alarm/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAlarm(Guid id)
        {
            var userId = GetUserId();
            var alarm = await _context.Alarms.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (alarm == null) return NotFound();

            _context.Alarms.Remove(alarm);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/Alarm/{id}/trigger (Manual trigger for testing SignalR)
        [HttpPost("{id}/trigger")]
        public async Task<IActionResult> TriggerAlarm(Guid id)
        {
            var userId = GetUserId();
            var alarm = await _context.Alarms.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (alarm == null) return NotFound();

            // Enviar evento de SignalR al grupo del usuario
            await _hubContext.Clients.Group(userId.ToString()).SendAsync("ReceiveAlarmTrigger", new
            {
                alarmId = alarm.Id,
                name = alarm.Name,
                type = alarm.Type.ToString(),
                audioId = alarm.AudioId,
                imageId = alarm.ImageId
            });

            return Ok(new { message = "Alarma disparada vía SignalR" });
        }
    }
}
