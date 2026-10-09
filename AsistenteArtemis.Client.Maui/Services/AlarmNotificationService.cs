using AsistenteArtemis.Client.Maui.Models.Dtos;
using AsistenteArtemis.Client.Maui.Models.Enums;
using Microsoft.AspNetCore.SignalR.Client;
using Plugin.Maui.Audio;

namespace AsistenteArtemis.Client.Maui.Services;

// Única fuente de verdad para "alarma sonando" (banner + cola de turnos de audio + Pausar/
// Detener) — antes esto vivía duplicado acá (versión "headless", solo audio/toast) y en
// Alarms.razor (versión con banner, atada al ciclo de vida de esa página). Se unificó acá
// porque esta conexión ya es a nivel de toda la app, sin importar qué pantalla esté abierta —
// el popup real (Components/Shared/AlarmPopup.razor) se renderiza desde MainLayout y solo lee
// este estado. Reemplaza el toast nativo de Windows (NativeNotifications.ShowToast, ya
// removido) — ver decisión del 2026-09-04 en el wiki.
public class AlarmNotificationService
{
    private readonly TokenStore _tokenStore;
    private readonly AuthState _authState;
    private readonly IAudioManager _audioManager;
    private readonly IAlarmApiClient _alarmApi;
    private HubConnection? _hubConnection;

    private readonly List<RingingAlarm> _ringingAlarms = new();
    private readonly Queue<(RingingAlarm Ringing, string Url, int? VolumePercent, bool IsPrimary, TimeSpan? MaxDuration)> _pendingAudio = new();

    // En los cambios de fase de Pausa Activa suena el audio propio solo este rato (a pedido
    // del usuario, 2026-10-09): avisa sin quedar sonando durante todo el descanso.
    private static readonly TimeSpan PhaseAudioMaxDuration = TimeSpan.FromSeconds(10);
    private bool _audioSlotBusy;

    public IReadOnlyList<RingingAlarm> RingingAlarms => _ringingAlarms;
    public bool IsConnected { get; private set; }

    // Un solo evento para "algo cambió que le importa a la UI" (sonó una alarma nueva, se
    // pausó/detuvo una, cambió el estado de conexión) — el popup y Alarms.razor simplemente
    // refrescan su vista, sin necesitar saber qué cambió puntualmente.
    public event Action? Changed;

    public RingingAlarm? this[Guid alarmId] => _ringingAlarms.FirstOrDefault(r => r.AlarmId == alarmId);

    public class RingingAlarm
    {
        public Guid AlarmId { get; init; }
        public string Name { get; init; } = string.Empty;
        // Referencia al reproductor real que quedó sonando — sin esto, Pausar/Detener solo
        // reprogramarían la alarma del lado del servidor pero el audio seguiría sonando solo.
        public ManagedAlarmAudio? Player { get; set; }
        // Ciclo automático trabajo/descanso (Pausa Activa habilitada): banner sin Pausar, chime
        // corto en vez del audio largo configurado, se autodescarta solo.
        public bool HasActiveBreak { get; init; }
        public string? PhaseJustEnded { get; init; } // "Work" | "Break"

        public string Message => NativeNotifications.BuildRingingMessage(Name, HasActiveBreak, PhaseJustEnded);
    }

    public AlarmNotificationService(TokenStore tokenStore, AuthState authState, IAudioManager audioManager, IAlarmApiClient alarmApi)
    {
        _tokenStore = tokenStore;
        _authState = authState;
        _audioManager = audioManager;
        _alarmApi = alarmApi;
        _authState.Changed += OnAuthChanged;

        if (_authState.IsAuthenticated)
        {
            _ = ConnectAsync();
        }
    }

    private void OnAuthChanged()
    {
        if (_authState.IsAuthenticated)
        {
            _ = ConnectAsync();
        }
        else
        {
            _ = DisconnectAsync();
        }
    }

    private async Task ConnectAsync()
    {
        if (_hubConnection is not null) return; // ya conectado

        var hubConnection = new HubConnectionBuilder()
            .WithUrl($"{ApiConfig.HubBaseUrl}/hubs/alarms", options =>
            {
                options.AccessTokenProvider = () => _tokenStore.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<AlarmTriggeredPayload>("ReceiveAlarmTrigger", payload =>
        {
            var ringing = new RingingAlarm
            {
                AlarmId = payload.AlarmId,
                Name = payload.Name,
                HasActiveBreak = payload.HasActiveBreak == true,
                PhaseJustEnded = payload.PhaseJustEnded
            };
            _ringingAlarms.Add(ringing);
            Changed?.Invoke();

            if (ringing.HasActiveBreak)
            {
                // Ciclo automático (Pausa Activa): el servidor ya reprogramó la siguiente fase
                // solo. Suena el audio propio recortado (o el chime si no hay audio) y el banner
                // se autodescarta.
                if (!string.IsNullOrEmpty(payload.AudioUrl))
                    EnqueueAudio(ringing, payload.AudioUrl, payload.Volume, PhaseAudioMaxDuration);
                _ = AutoDismissPhaseBannerAsync(ringing, playChime: string.IsNullOrEmpty(payload.AudioUrl));
            }
            else if (!string.IsNullOrEmpty(payload.AudioUrl))
            {
                EnqueueAudio(ringing, payload.AudioUrl, payload.Volume, maxDuration: null);
            }
        });

        hubConnection.Reconnected += _ => { IsConnected = true; Changed?.Invoke(); return Task.CompletedTask; };
        hubConnection.Reconnecting += _ => { IsConnected = false; Changed?.Invoke(); return Task.CompletedTask; };
        hubConnection.Closed += _ => { IsConnected = false; Changed?.Invoke(); return Task.CompletedTask; };

        _hubConnection = hubConnection;

        try
        {
            await hubConnection.StartAsync();
            IsConnected = true;
            Changed?.Invoke();
        }
        catch
        {
            // Best-effort — si no conecta ahora, no hay banner/audio hasta el próximo cambio de
            // AuthState (login/logout) que reintente ConnectAsync.
            _hubConnection = null;
            IsConnected = false;
            Changed?.Invoke();
        }
    }

    private async Task DisconnectAsync()
    {
        if (_hubConnection is null) return;
        var hub = _hubConnection;
        _hubConnection = null;
        IsConnected = false;
        await hub.DisposeAsync();
        Changed?.Invoke();
    }

    // Primaria = nadie sonando ni esperando turno ahora mismo — entra directo, sin fade-in. Si
    // no, espera su turno y entra suave (ver decisión "cola de turnos").
    private void EnqueueAudio(RingingAlarm ringing, string url, int? volumePercent, TimeSpan? maxDuration)
    {
        var isPrimary = !_audioSlotBusy && _pendingAudio.Count == 0;
        _pendingAudio.Enqueue((ringing, url, volumePercent, isPrimary, maxDuration));
        _ = PlayNextQueuedAudioAsync();
    }

    // Transiciones de Pausa Activa: chime corto si la alarma no tiene audio propio. El banner
    // queda visible 15s (a pedido del usuario — con menos no le daba tiempo a leer en qué fase
    // está) antes de autodescartarse solo, sin tocar el servidor (a diferencia de Detener).
    private async Task AutoDismissPhaseBannerAsync(RingingAlarm ringing, bool playChime)
    {
        try
        {
#if WINDOWS
            if (playChime)
            {
                // Console.Beep en vez de MessageBeep/SystemSounds: genera el tono directo, sin
                // depender del "esquema de sonidos" de Windows ni de paquetes extra. Bloquea el
                // hilo que lo llama por la duración total, por eso va en un Task.Run.
                await Task.Run(() =>
                {
                    Console.Beep(880, 150);
                    Console.Beep(1046, 150);
                });
            }
#endif
            await Task.Delay(TimeSpan.FromSeconds(15));
        }
        catch
        {
            // Best-effort, mismo criterio que el resto de la reproducción de audio acá.
        }
        finally
        {
            StopPlayback(ringing);
            _ringingAlarms.Remove(ringing);
            Changed?.Invoke();
        }
    }

    // Solo un audio suena por vez — si ya hay otra sonando, esta queda en cola hasta que la
    // anterior se pause/detenga o termine sola; el banner de "sonó la alarma" sí aparece de
    // inmediato para todas, sin esperar el turno del audio (ver decisión "cola de turnos").
    private async Task PlayNextQueuedAudioAsync()
    {
        if (_audioSlotBusy) return;
        if (!_pendingAudio.TryDequeue(out var next)) return;

        if (!_ringingAlarms.Contains(next.Ringing))
        {
            // Se pausó/detuvo mientras esperaba su turno en cola — probar la siguiente.
            _ = PlayNextQueuedAudioAsync();
            return;
        }

        _audioSlotBusy = true;
        var playerCreated = false;
        try
        {
            await PlayAlarmSoundAsync(next.Ringing, next.Url, next.IsPrimary, next.VolumePercent, next.MaxDuration, () => playerCreated = true);
        }
        finally
        {
            // Si PlayAlarmSoundAsync nunca llegó a crear el player (descarga fallida, audio
            // roto, etc.), nadie va a llamar Stop para liberar el turno — cubrir ese caso acá
            // para que la cola no quede trabada para siempre.
            if (!playerCreated)
            {
                _audioSlotBusy = false;
                _ = PlayNextQueuedAudioAsync();
            }
        }
    }

    // Reproducción best-effort: un audio roto, sin red, o un formato no soportado no debe
    // romper el banner de la alarma. La mecánica real (descarga+caché, fix de seek, guard de
    // stack overflow) vive en ManagedAlarmAudio.
    private async Task PlayAlarmSoundAsync(RingingAlarm ringing, string url, bool isPrimary, int? volumePercent,
        TimeSpan? maxDuration, Action onPlayerCreated)
    {
        try
        {
            // Si ya se apretó Pausar/Detener mientras esperábamos nuestro turno en cola, no
            // arrancar a sonar igual.
            if (!_ringingAlarms.Contains(ringing)) return;

            var managed = new ManagedAlarmAudio();
            managed.Ended += () =>
            {
                _audioSlotBusy = false;
                _ = PlayNextQueuedAudioAsync();
            };

            await managed.PlayAsync(_audioManager, url, volumePercent, fadeIn: !isPrimary,
                shouldContinue: () => _ringingAlarms.Contains(ringing),
                onPlayerCreated: () =>
                {
                    ringing.Player = managed;
                    onPlayerCreated();
                    Changed?.Invoke(); // saca "(esperando turno de audio)" ni bien arranca
                    if (maxDuration is { } limit)
                    {
                        // Stop() es idempotente: si ya terminó sola o la detuvieron, no pasa nada.
                        _ = Task.Delay(limit).ContinueWith(_ => managed.Stop());
                    }
                });
        }
        catch
        {
            // Silencioso a propósito, ver comentario arriba.
        }
    }

    private static void StopPlayback(RingingAlarm ringing) => ringing.Player?.Stop();

    // Detener/Pausar del popup de "sonó la alarma" — cada banner es independiente (ver
    // RingingAlarms) para el caso de dos alarmas cruzadas.
    public async Task StopRingingAlarmAsync(Guid alarmId)
    {
        var ringing = this[alarmId];
        if (ringing is null) return;

        StopPlayback(ringing);
        _ringingAlarms.Remove(ringing);
        Changed?.Invoke();
        await _alarmApi.UpdateAlarmAsync(alarmId, new AlarmUpdateDto { IsActive = false });
    }

    // Con "Pausa activa" configurada (SnoozeMinutes), Pausar entra en ese descanso y al
    // terminar retoma el ciclo normal de DurationMinutes. Sin pausa activa configurada, Pausar
    // simplemente repite el propio intervalo de la alarma (DurationMinutes) — "que vuelva a
    // sonar dentro de 30 minutos, la configuración que le hice".
    public async Task PauseRingingAlarmAsync(Guid alarmId)
    {
        var ringing = this[alarmId];
        if (ringing is null) return;

        StopPlayback(ringing);
        _ringingAlarms.Remove(ringing);
        Changed?.Invoke();

        var alarms = await _alarmApi.GetAlarmsAsync();
        var alarm = alarms.FirstOrDefault(a => a.Id == alarmId);
        var minutes = alarm?.SnoozeMinutes
            ?? (alarm?.Type == AlarmType.Pomodoro ? alarm?.DurationMinutes : null)
            ?? 5;

        await _alarmApi.UpdateAlarmAsync(alarmId, new AlarmUpdateDto
        {
            IsActive = true,
            ScheduleAt = DateTime.UtcNow.AddMinutes(minutes)
        });
    }
}
