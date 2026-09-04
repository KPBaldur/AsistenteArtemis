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

        private const int DefaultPomodoroMinutes = 25;

        // Ventanas que cruzan medianoche (ej. 22:00-02:00) no están soportadas en esta pasada.
        private static bool IsValidWindow(TimeOnly? start, TimeOnly? end) =>
            start.HasValue == end.HasValue && (!start.HasValue || start.Value < end!.Value);

        private static int? ClampVolume(int? volume) => volume.HasValue ? Math.Clamp(volume.Value, 0, 100) : null;

        private static AlarmDto ToDto(Alarm a) => new AlarmDto
        {
            Id = a.Id,
            Name = a.Name,
            Type = a.Type,
            DurationMinutes = a.DurationMinutes,
            ScheduleAt = a.ScheduleAt,
            IsActive = a.IsActive,
            AudioId = a.AudioId,
            ImageId = a.ImageId,
            DaysOfWeek = a.DaysOfWeek,
            WindowStart = a.WindowStart,
            WindowEnd = a.WindowEnd,
            SnoozeMinutes = a.SnoozeMinutes,
            Volume = a.Volume
        };

        // GET: api/Alarm
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AlarmDto>>> GetAlarms()
        {
            var userId = GetUserId();
            var alarms = await _context.Alarms
                .Where(a => a.UserId == userId)
                .ToListAsync();

            return Ok(alarms.Select(ToDto));
        }

        // POST: api/Alarm
        [HttpPost]
        public async Task<ActionResult<AlarmDto>> CreateAlarm(AlarmCreateDto createDto)
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(createDto.Name))
                return BadRequest(new { message = "El nombre es obligatorio" });

            if (!IsValidWindow(createDto.WindowStart, createDto.WindowEnd))
                return BadRequest(new { message = "La ventana horaria necesita Desde y Hasta, con Desde antes que Hasta" });

            DateTime? scheduleAt;
            int? durationMinutes = null;
            var daysOfWeek = AlarmDaysOfWeek.All;
            TimeOnly? windowStart = null;
            TimeOnly? windowEnd = null;
            int? snoozeMinutes = null;

            if (createDto.Type == AlarmType.Pomodoro)
            {
                durationMinutes = createDto.DurationMinutes is > 0 ? createDto.DurationMinutes : DefaultPomodoroMinutes;
                scheduleAt = DateTime.UtcNow.AddMinutes(durationMinutes.Value);
                daysOfWeek = createDto.DaysOfWeek is > AlarmDaysOfWeek.None ? createDto.DaysOfWeek.Value : AlarmDaysOfWeek.All;
                windowStart = createDto.WindowStart;
                windowEnd = createDto.WindowEnd;
                // Opcional a propósito: sin pausa configurada, la alarma solo ofrece "Detener" al sonar.
                snoozeMinutes = createDto.SnoozeMinutes is > 0 ? createDto.SnoozeMinutes : null;
            }
            else
            {
                if (createDto.ScheduleAt == null)
                    return BadRequest(new { message = "Fecha requerida para alarma datetime" });
                if (createDto.ScheduleAt <= DateTime.UtcNow)
                    return BadRequest(new { message = "La fecha de la alarma debe ser futura" });
                scheduleAt = createDto.ScheduleAt;
            }

            var alarm = new Alarm
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = createDto.Name.Trim(),
                Type = createDto.Type,
                DurationMinutes = durationMinutes,
                ScheduleAt = scheduleAt,
                AudioId = createDto.AudioId,
                ImageId = createDto.ImageId,
                IsActive = true,
                DaysOfWeek = daysOfWeek,
                WindowStart = windowStart,
                WindowEnd = windowEnd,
                SnoozeMinutes = snoozeMinutes,
                Volume = ClampVolume(createDto.Volume)
            };

            _context.Alarms.Add(alarm);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAlarms), new { id = alarm.Id }, ToDto(alarm));
        }

        // PUT: api/Alarm/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAlarm(Guid id, AlarmUpdateDto updateDto)
        {
            var userId = GetUserId();
            var alarm = await _context.Alarms.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (alarm == null) return NotFound();

            if (!IsValidWindow(updateDto.WindowStart, updateDto.WindowEnd))
                return BadRequest(new { message = "La ventana horaria necesita Desde y Hasta, con Desde antes que Hasta" });

            if (updateDto.Name != null) alarm.Name = updateDto.Name.Trim();
            if (updateDto.AudioId.HasValue) alarm.AudioId = updateDto.AudioId;
            if (updateDto.ImageId.HasValue) alarm.ImageId = updateDto.ImageId;
            if (updateDto.DaysOfWeek.HasValue) alarm.DaysOfWeek = updateDto.DaysOfWeek.Value;
            if (updateDto.Volume.HasValue) alarm.Volume = ClampVolume(updateDto.Volume);
            if (updateDto.ClearSnoozeMinutes) alarm.SnoozeMinutes = null;
            else if (updateDto.SnoozeMinutes is > 0) alarm.SnoozeMinutes = updateDto.SnoozeMinutes;
            if (updateDto.ClearWindow)
            {
                alarm.WindowStart = null;
                alarm.WindowEnd = null;
            }
            else if (updateDto.WindowStart.HasValue)
            {
                alarm.WindowStart = updateDto.WindowStart;
                alarm.WindowEnd = updateDto.WindowEnd;
            }

            var reactivating = updateDto.IsActive == true && !alarm.IsActive;
            var durationChanged = updateDto.DurationMinutes.HasValue && updateDto.DurationMinutes != alarm.DurationMinutes;

            if (updateDto.DurationMinutes.HasValue) alarm.DurationMinutes = updateDto.DurationMinutes;
            if (updateDto.IsActive.HasValue) alarm.IsActive = updateDto.IsActive.Value;

            if (!alarm.IsActive)
            {
                // Desactivada: no debe volver a sonar hasta que se reactive explícitamente.
                alarm.ScheduleAt = null;
            }
            else if (updateDto.ScheduleAt.HasValue)
            {
                // Cambio explícito de horario (ej: snooze).
                alarm.ScheduleAt = updateDto.ScheduleAt;
            }
            else if (alarm.Type == AlarmType.Pomodoro && (reactivating || durationChanged))
            {
                // Reactivación o cambio de duración: reinicia el conteo desde ahora.
                var duration = alarm.DurationMinutes is > 0 ? alarm.DurationMinutes.Value : DefaultPomodoroMinutes;
                alarm.ScheduleAt = DateTime.UtcNow.AddMinutes(duration);
            }

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
            var alarm = await _context.Alarms.Include(a => a.Audio).FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (alarm == null) return NotFound();

            // Enviar evento de SignalR al grupo del usuario
            await _hubContext.Clients.Group(userId.ToString()).SendAsync("ReceiveAlarmTrigger", new
            {
                alarmId = alarm.Id,
                name = alarm.Name,
                type = alarm.Type.ToString(),
                audioId = alarm.AudioId,
                imageId = alarm.ImageId,
                audioUrl = alarm.Audio?.Url,
                volume = alarm.Volume
            });

            return Ok(new { message = "Alarma disparada vía SignalR" });
        }
    }
}
