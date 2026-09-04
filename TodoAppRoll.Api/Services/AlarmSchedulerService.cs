using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TodoAppRoll.Api.Data;
using TodoAppRoll.Api.Hubs;
using TodoAppRoll.Api.Models.Enums;
using Alarm = TodoAppRoll.Api.Models.Entities.Alarm;

namespace TodoAppRoll.Api.Services
{
    /// <summary>
    /// Revisa periódicamente las alarmas activas vencidas y las dispara vía SignalR.
    /// Pomodoro se reprograma automáticamente (+DurationMinutes); DateTime se desactiva
    /// después de sonar una vez. Equivalente al schedulerService.ts del legacy, pero por
    /// polling en vez de un setTimeout por alarma (más simple y sobrevive a reinicios/redeploys).
    /// </summary>
    public class AlarmSchedulerService : BackgroundService
    {
        // 15s original generaba un desfase notorio (hasta 15s, ~7.5s en promedio) entre la hora
        // real de la alarma y el disparo — bajado a 3s: app personal de un solo usuario, el costo
        // extra de consultar la DB más seguido no importa acá.
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
        private const int DefaultPomodoroMinutes = 25;

        // App personal de un solo usuario en Chile — hora fija en vez de manejar timezone por usuario.
        // Chile no usa horario de verano desde 2016, así que no hace falta lidiar con DST acá.
        private static readonly TimeZoneInfo ChileTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AlarmSchedulerService> _logger;

        public AlarmSchedulerService(IServiceScopeFactory scopeFactory, ILogger<AlarmSchedulerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TriggerDueAlarmsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error revisando alarmas vencidas");
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
        }

        private async Task TriggerDueAlarmsAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AlarmHub>>();

            var now = DateTime.UtcNow;
            var dueAlarms = await context.Alarms
                .Include(a => a.Audio)
                .Where(a => a.IsActive && a.ScheduleAt != null && a.ScheduleAt <= now)
                .ToListAsync(stoppingToken);

            foreach (var alarm in dueAlarms)
            {
                if (alarm.Type == AlarmType.Pomodoro && !IsWithinSchedule(alarm, now))
                {
                    // Fuera de los días/horario configurados: no suena, se reintenta el próximo ciclo.
                    var duration = alarm.DurationMinutes is > 0 ? alarm.DurationMinutes.Value : DefaultPomodoroMinutes;
                    alarm.ScheduleAt = now.AddMinutes(duration);
                    continue;
                }

                _logger.LogInformation("Disparando alarma '{Name}' ({Type}) del usuario {UserId}", alarm.Name, alarm.Type, alarm.UserId);

                await hubContext.Clients.Group(alarm.UserId.ToString()).SendAsync("ReceiveAlarmTrigger", new
                {
                    alarmId = alarm.Id,
                    name = alarm.Name,
                    type = alarm.Type.ToString(),
                    audioId = alarm.AudioId,
                    imageId = alarm.ImageId,
                    audioUrl = alarm.Audio?.Url,
                    volume = alarm.Volume
                }, stoppingToken);

                if (alarm.Type == AlarmType.Pomodoro)
                {
                    // No se reprograma sola acá: se queda "congelada" (ScheduleAt en null,
                    // IsActive se mantiene en true) hasta que el usuario la reconozca desde el
                    // banner — Pausar recién ahí reinicia el conteo desde 0 (AlarmController.
                    // UpdateAlarm, rama de ScheduleAt explícito), Detener la desactiva. Antes se
                    // reprogramaba sola de inmediato acá, así que si el usuario tardaba más que
                    // su propio DurationMinutes en reaccionar, volvía a sonar (y a competir por
                    // el turno de audio del lado del cliente) sin que nadie la haya reconocido.
                    alarm.ScheduleAt = null;
                }
                else
                {
                    alarm.IsActive = false;
                    alarm.ScheduleAt = null;
                }
            }

            if (dueAlarms.Count > 0)
            {
                await context.SaveChangesAsync(stoppingToken);
            }
        }

        // Sin ventana configurada (WindowStart/WindowEnd null) = suena todo el tiempo.
        private static bool IsWithinSchedule(Alarm alarm, DateTime utcNow)
        {
            if (!alarm.WindowStart.HasValue || !alarm.WindowEnd.HasValue)
                return true;

            var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, ChileTimeZone);
            var today = localNow.DayOfWeek switch
            {
                DayOfWeek.Monday => AlarmDaysOfWeek.Monday,
                DayOfWeek.Tuesday => AlarmDaysOfWeek.Tuesday,
                DayOfWeek.Wednesday => AlarmDaysOfWeek.Wednesday,
                DayOfWeek.Thursday => AlarmDaysOfWeek.Thursday,
                DayOfWeek.Friday => AlarmDaysOfWeek.Friday,
                DayOfWeek.Saturday => AlarmDaysOfWeek.Saturday,
                DayOfWeek.Sunday => AlarmDaysOfWeek.Sunday,
                _ => AlarmDaysOfWeek.None
            };

            if (!alarm.DaysOfWeek.HasFlag(today))
                return false;

            var timeOfDay = TimeOnly.FromDateTime(localNow);
            return timeOfDay >= alarm.WindowStart.Value && timeOfDay <= alarm.WindowEnd.Value;
        }
    }
}
