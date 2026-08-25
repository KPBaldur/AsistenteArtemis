using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TodoAppRoll.Api.Data;
using TodoAppRoll.Api.Hubs;
using TodoAppRoll.Api.Models.Enums;

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
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
        private const int DefaultPomodoroMinutes = 25;

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
                .Where(a => a.IsActive && a.ScheduleAt != null && a.ScheduleAt <= now)
                .ToListAsync(stoppingToken);

            foreach (var alarm in dueAlarms)
            {
                _logger.LogInformation("Disparando alarma '{Name}' ({Type}) del usuario {UserId}", alarm.Name, alarm.Type, alarm.UserId);

                await hubContext.Clients.Group(alarm.UserId.ToString()).SendAsync("ReceiveAlarmTrigger", new
                {
                    alarmId = alarm.Id,
                    name = alarm.Name,
                    type = alarm.Type.ToString(),
                    audioId = alarm.AudioId,
                    imageId = alarm.ImageId
                }, stoppingToken);

                if (alarm.Type == AlarmType.Pomodoro)
                {
                    var duration = alarm.DurationMinutes is > 0 ? alarm.DurationMinutes.Value : DefaultPomodoroMinutes;
                    alarm.ScheduleAt = now.AddMinutes(duration);
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
    }
}
