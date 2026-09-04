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
                // La ventana de días/horario solo aplica para arrancar/continuar la fase de
                // trabajo — un descanso ya empezado (IsInBreak) no se interrumpe por la ventana,
                // son minutos cortos y ya en curso.
                if (alarm.Type == AlarmType.Pomodoro && !alarm.IsInBreak && !IsWithinSchedule(alarm, now))
                {
                    // Fuera de los días/horario configurados: no suena, se reintenta el próximo ciclo.
                    var duration = alarm.DurationMinutes is > 0 ? alarm.DurationMinutes.Value : DefaultPomodoroMinutes;
                    alarm.ScheduleAt = now.AddMinutes(duration);
                    continue;
                }

                _logger.LogInformation("Disparando alarma '{Name}' ({Type}) del usuario {UserId}", alarm.Name, alarm.Type, alarm.UserId);

                // Capturado antes de mutar alarm.IsInBreak más abajo — el cliente necesita saber
                // qué fase acaba de terminar para mostrar el mensaje correcto.
                var hasActiveBreak = alarm.SnoozeMinutes is > 0;
                var phaseJustEnded = alarm.IsInBreak ? "Break" : "Work";

                await hubContext.Clients.Group(alarm.UserId.ToString()).SendAsync("ReceiveAlarmTrigger", new
                {
                    alarmId = alarm.Id,
                    name = alarm.Name,
                    type = alarm.Type.ToString(),
                    audioId = alarm.AudioId,
                    imageId = alarm.ImageId,
                    audioUrl = alarm.Audio?.Url,
                    volume = alarm.Volume,
                    hasActiveBreak,
                    phaseJustEnded
                }, stoppingToken);

                if (alarm.Type == AlarmType.Pomodoro)
                {
                    if (!hasActiveBreak)
                    {
                        // Sin Pausa Activa: se queda "congelada" (ScheduleAt en null, IsActive
                        // se mantiene en true) hasta que el usuario la reconozca desde el banner
                        // — Pausar recién ahí reinicia el conteo desde 0 (AlarmController.
                        // UpdateAlarm, rama de ScheduleAt explícito), Detener la desactiva.
                        alarm.ScheduleAt = null;
                    }
                    else if (!alarm.IsInBreak)
                    {
                        // Terminó el trabajo -> arranca el descanso solo, sin acción del usuario.
                        alarm.IsInBreak = true;
                        alarm.ScheduleAt = now.AddMinutes(alarm.SnoozeMinutes!.Value);
                    }
                    else
                    {
                        // Terminó el descanso -> arranca el trabajo solo, sin acción del usuario.
                        alarm.IsInBreak = false;
                        var duration = alarm.DurationMinutes is > 0 ? alarm.DurationMinutes.Value : DefaultPomodoroMinutes;
                        alarm.ScheduleAt = now.AddMinutes(duration);
                    }
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
