using AsistenteArtemis.Client.Maui.Models.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using Plugin.Maui.Audio;

namespace AsistenteArtemis.Client.Maui.Services;

// Conexión de SignalR propia, a nivel de toda la app (no atada al ciclo de vida de ninguna
// página) — existe para mandar la notificación nativa de Windows y reproducir el sonido real de
// la alarma sin importar en qué pantalla esté el usuario. Alarms.razor tiene su propia conexión
// y su propia cola de turnos para cuando esa página SÍ está abierta (con banner, botones
// Pausar/Detener, etc.) — deliberadamente no centralizado todo en un solo lugar, para no tener
// que migrar ese pipeline ya verificado. IsAlarmsPageActive es la señal que evita que las dos
// conexiones reproduzcan el mismo audio dos veces cuando Alarms.razor sí está abierta.
public class AlarmNotificationService
{
    private readonly TokenStore _tokenStore;
    private readonly AuthState _authState;
    private readonly IAudioManager _audioManager;
    private HubConnection? _hubConnection;

    private readonly Queue<(string Url, int? VolumePercent, bool IsPrimary)> _pendingAudio = new();
    private bool _audioSlotBusy;

    public bool IsAlarmsPageActive { get; set; }

    public AlarmNotificationService(TokenStore tokenStore, AuthState authState, IAudioManager audioManager)
    {
        _tokenStore = tokenStore;
        _authState = authState;
        _audioManager = audioManager;
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
            var message = NativeNotifications.BuildRingingMessage(payload.Name, payload.HasActiveBreak == true, payload.PhaseJustEnded);
            NativeNotifications.ShowToast(message);

            if (IsAlarmsPageActive) return; // esa página ya se encarga del audio

            if (payload.HasActiveBreak == true)
            {
                _ = PlayChimeAsync();
            }
            else if (!string.IsNullOrEmpty(payload.AudioUrl))
            {
                var isPrimary = !_audioSlotBusy && _pendingAudio.Count == 0;
                _pendingAudio.Enqueue((payload.AudioUrl, payload.Volume, isPrimary));
                _ = PlayNextQueuedAudioAsync();
            }
        });

        _hubConnection = hubConnection;

        try
        {
            await hubConnection.StartAsync();
        }
        catch
        {
            // Best-effort — si no conecta ahora, no hay toast/audio hasta el próximo cambio de
            // AuthState (login/logout) que reintente ConnectAsync.
            _hubConnection = null;
        }
    }

    private async Task DisconnectAsync()
    {
        if (_hubConnection is null) return;
        var hub = _hubConnection;
        _hubConnection = null;
        await hub.DisposeAsync();
    }

    // Chime corto de las transiciones de Pausa Activa (mismo Console.Beep que Alarms.razor usa
    // en su banner) — acá no hace falta autodescartar ningún banner, solo sonar.
    private static async Task PlayChimeAsync()
    {
#if WINDOWS
        try
        {
            await Task.Run(() =>
            {
                Console.Beep(880, 150);
                Console.Beep(1046, 150);
            });
        }
        catch
        {
        }
#else
        await Task.CompletedTask;
#endif
    }

    // Cola simple (sin banner, sin lista de "ringing") — evita que dos alarmas se mezclen
    // estando lejos de Alarms.razor. Misma mecánica de audio (ManagedAlarmAudio) que esa página:
    // Ended (disparado cuando el clip termina solo o alguien llama Stop) libera el turno; si
    // PlayAsync nunca llega a crear el reproductor (descarga fallida, etc.), Ended no se dispara
    // solo, así que el fallback en el finally (playerCreated) cubre ese caso — sin él la cola
    // quedaría trabada para siempre.
    private async Task PlayNextQueuedAudioAsync()
    {
        if (_audioSlotBusy) return;
        if (!_pendingAudio.TryDequeue(out var next)) return;

        _audioSlotBusy = true;
        var playerCreated = false;
        try
        {
            var managed = new ManagedAlarmAudio();
            managed.Ended += () =>
            {
                _audioSlotBusy = false;
                _ = PlayNextQueuedAudioAsync();
            };

            await managed.PlayAsync(_audioManager, next.Url, next.VolumePercent, fadeIn: !next.IsPrimary,
                onPlayerCreated: () => playerCreated = true);
        }
        catch
        {
            // Best-effort, mismo criterio que PlayAlarmSoundAsync en Alarms.razor.
        }
        finally
        {
            if (!playerCreated)
            {
                _audioSlotBusy = false;
                _ = PlayNextQueuedAudioAsync();
            }
        }
    }
}
