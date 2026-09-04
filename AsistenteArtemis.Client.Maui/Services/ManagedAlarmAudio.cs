using Plugin.Maui.Audio;

namespace AsistenteArtemis.Client.Maui.Services;

// Reproduce un clip de alarma remoto (Cloudinary) — extraído de Alarms.razor sin cambiar su
// lógica interna, para que tanto la página de Alarmas como AlarmNotificationService (audio
// "headless" cuando el usuario no está en esa página) compartan exactamente el mismo código ya
// verificado, en vez de duplicarlo. Encapsula tres cosas ganadas a los golpes en la sesión de
// hoy, todas juntas:
// - MemoryStream en vez del stream de red crudo: en WinUI el reproductor necesita envolverlo
//   como IRandomAccessStream, que exige soporte de seek (el stream de red no lo tiene).
// - Caché en memoria por URL, compartida entre todas las instancias — evita redescargar el
//   mismo audio de Cloudinary en cada disparo de una alarma recurrente.
// - Guard de una sola ejecución (_disposed) en Stop(): Plugin.Maui.Audio en WinUI dispara
//   PlaybackEnded también al llamar Stop() manualmente (no solo al terminar solo), y su propio
//   Dispose() llama a Stop() por dentro — sin este guard, Dispose -> Stop -> PlaybackEnded ->
//   Dispose (de nuevo) entra en loop infinito y revienta la pila (stack overflow real).
public class ManagedAlarmAudio
{
    private static readonly Dictionary<string, byte[]> Cache = new();

    private bool _disposed;
    private IAudioPlayer? _player;
    private MemoryStream? _stream;

    // Se dispara una sola vez, sea porque el clip terminó solo o porque alguien llamó Stop().
    public event Action? Ended;

    // shouldContinue: chequeo opcional justo antes de crear el reproductor (además del interno
    // vía Stop()/_disposed) — el llamador lo usa para abortar si, por ejemplo, el usuario ya
    // apretó Detener/Pausar mientras se descargaba pero todavía no había reproductor que parar.
    public async Task PlayAsync(IAudioManager audioManager, string url, int? volumePercent, bool fadeIn,
        Func<bool>? shouldContinue = null, Action? onPlayerCreated = null)
    {
        var targetVolume = Math.Clamp((volumePercent ?? 100) / 100.0, 0.0, 1.0);

        if (!Cache.TryGetValue(url, out var bytes))
        {
            using var http = new HttpClient();
            bytes = await http.GetByteArrayAsync(url);
            Cache[url] = bytes;
        }

        if (_disposed) return; // Stop() ya se llamó mientras descargábamos.
        if (shouldContinue is not null && !shouldContinue()) return;

        var stream = new MemoryStream(bytes);
        var player = audioManager.CreatePlayer(stream);
        player.PlaybackEnded += (_, _) => Stop();

        _player = player;
        _stream = stream;
        onPlayerCreated?.Invoke();

        if (!fadeIn)
        {
            player.Volume = targetVolume;
            player.Play();
        }
        else
        {
            player.Volume = 0;
            player.Play();
            for (var step = 1; step <= 10; step++)
            {
                await Task.Delay(200);
                if (_disposed) return;
                player.Volume = targetVolume * step / 10.0;
            }
        }
    }

    // Dispose() ya detiene la reproducción por dentro — no hace falta llamar a un Stop nativo
    // aparte. Idempotente a propósito (ver comentario de clase).
    public void Stop()
    {
        if (_disposed) return;
        _disposed = true;
        _player?.Dispose();
        _stream?.Dispose();
        Ended?.Invoke();
    }
}
