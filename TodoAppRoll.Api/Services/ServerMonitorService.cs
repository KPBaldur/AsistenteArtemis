using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Diagnostics;
using TodoAppRoll.Api.Data;
using TodoAppRoll.Api.Hubs;
using TodoAppRoll.Api.Models.Entities;
using TodoAppRoll.Api.Models.Enums;
using Task = System.Threading.Tasks.Task;

namespace TodoAppRoll.Api.Services
{
    /// <summary>
    /// Revisa periódicamente los monitores activos (Fase 5: monitoreo de servidores).
    /// Un HttpPing cuenta cualquier respuesta HTTP recibida como "Up" (incluso 4xx/5xx) —
    /// solo un timeout o error de conexión cuenta como "Down": los servicios gratuitos de
    /// Render duermen cuando están inactivos y pueden tardar decenas de segundos en
    /// responder al despertar, así que el timeout es deliberadamente generoso (45s).
    /// </summary>
    public class ServerMonitorService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
        private const int PostgresTimeoutSeconds = 10;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ServerMonitorService> _logger;

        public ServerMonitorService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, ILogger<ServerMonitorService> logger)
        {
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAllMonitorsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error revisando monitores de servidores");
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
        }

        private async Task CheckAllMonitorsAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<MonitorHub>>();

            var monitors = await context.Monitors
                .Where(m => m.IsEnabled)
                .ToListAsync(stoppingToken);

            if (monitors.Count == 0)
            {
                return;
            }

            // Los chequeos de red corren en paralelo (no tocan el DbContext, que no es
            // thread-safe); los resultados se aplican después, uno por uno, sobre el
            // mismo contexto de forma secuencial.
            var checkTasks = monitors.Select(async monitor =>
            {
                var result = monitor.Type == MonitorCheckType.HttpPing
                    ? await CheckHttpAsync(monitor.Target, stoppingToken)
                    : await CheckPostgresAsync(monitor.Target, configuration, stoppingToken);
                return (Monitor: monitor, Result: result);
            });

            var results = await Task.WhenAll(checkTasks);

            foreach (var (monitor, (status, responseTimeMs, errorMessage)) in results)
            {
                monitor.LastStatus = status;
                monitor.LastCheckedAt = DateTime.UtcNow;
                monitor.LastResponseTimeMs = responseTimeMs;
                monitor.LastErrorMessage = errorMessage;

                context.MonitorCheckResults.Add(new MonitorCheckResult
                {
                    Id = Guid.NewGuid(),
                    MonitorId = monitor.Id,
                    CheckedAt = monitor.LastCheckedAt.Value,
                    Status = status,
                    ResponseTimeMs = responseTimeMs,
                    ErrorMessage = errorMessage
                });

                _logger.LogInformation("Monitor '{Name}' ({Type}) -> {Status} ({ResponseTimeMs}ms)", monitor.Name, monitor.Type, status, responseTimeMs);
            }

            await context.SaveChangesAsync(stoppingToken);

            await Task.WhenAll(results.Select(r => hubContext.Clients.Group(r.Monitor.UserId.ToString()).SendAsync("MonitorStatusChanged", new
            {
                monitorId = r.Monitor.Id,
                name = r.Monitor.Name,
                status = r.Result.Status.ToString(),
                checkedAt = r.Monitor.LastCheckedAt,
                responseTimeMs = r.Result.ResponseTimeMs,
                errorMessage = r.Result.ErrorMessage
            }, stoppingToken)));
        }

        private async Task<(MonitorStatus Status, int? ResponseTimeMs, string? ErrorMessage)> CheckHttpAsync(string url, CancellationToken stoppingToken)
        {
            var client = _httpClientFactory.CreateClient("MonitorPing");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var response = await client.GetAsync(url, stoppingToken);
                stopwatch.Stop();
                // Cualquier respuesta HTTP (incluso 4xx/5xx) prueba que el proceso está vivo.
                return (MonitorStatus.Up, (int)stopwatch.ElapsedMilliseconds, null);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return (MonitorStatus.Down, (int)stopwatch.ElapsedMilliseconds, ex.Message);
            }
        }

        private async Task<(MonitorStatus Status, int? ResponseTimeMs, string? ErrorMessage)> CheckPostgresAsync(string connectionStringKey, IConfiguration configuration, CancellationToken stoppingToken)
        {
            var connectionString = configuration.GetConnectionString(connectionStringKey);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return (MonitorStatus.Unknown, null, $"Connection string '{connectionStringKey}' no configurada");
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(TimeSpan.FromSeconds(PostgresTimeoutSeconds));

                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync(cts.Token);

                await using var cmd = new NpgsqlCommand("SELECT 1", conn);
                await cmd.ExecuteScalarAsync(cts.Token);

                stopwatch.Stop();
                return (MonitorStatus.Up, (int)stopwatch.ElapsedMilliseconds, null);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return (MonitorStatus.Down, (int)stopwatch.ElapsedMilliseconds, ex.Message);
            }
        }
    }
}
