using AsistenteArtemis.Client.Maui.Models.Dtos;

namespace AsistenteArtemis.Client.Maui.Services;

// Conexión de la mascota de escritorio con la cuenta: un token de dispositivo propio, que la
// app deja en el api.cfg de Godot (ver MascotLink). EnsureConnectedAsync corre solo al
// iniciar sesión (MainLayout), a pedido del usuario: la mascota queda conectada sin pasar por
// Identidad. Identidad conserva los botones para reconectar o revocar a mano.
public class MascotConnector(IDeviceTokenApiClient deviceTokenApi)
{
    private bool _checkedThisSession;

    public async Task<List<DeviceTokenDto>> GetActiveTokensAsync() =>
        (await deviceTokenApi.GetDeviceTokensAsync())
            .Where(d => d.Name == MascotLink.DeviceName && d.RevokedAt is null)
            .ToList();

    // Conecta solo si hace falta: sin api.cfg o sin token activo en la Api (por ejemplo, si se
    // revocó desde otro equipo). Una vez por sesión, y silencioso: si falla, la mascota avisa
    // "no estoy conectada" y queda el botón manual en Identidad.
    public async Task EnsureConnectedAsync()
    {
        if (_checkedThisSession)
            return;
        _checkedThisSession = true;
        try
        {
            var active = await GetActiveTokensAsync();
            if (active.Count > 0 && MascotLink.IsLinked)
                return;
            await ReconnectAsync(active);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            _checkedThisSession = false; // reintentar en el próximo inicio de sesión
        }
    }

    // Un solo token activo por mascota: los anteriores se revocan.
    public async Task ReconnectAsync(IEnumerable<DeviceTokenDto>? active = null)
    {
        foreach (var old in active ?? await GetActiveTokensAsync())
            await deviceTokenApi.RevokeDeviceTokenAsync(old.Id);

        var created = await deviceTokenApi.CreateDeviceTokenAsync(new DeviceTokenCreateDto { Name = MascotLink.DeviceName });
        MascotLink.Write(created.Token);
    }

    public async Task DisconnectAsync()
    {
        foreach (var device in await GetActiveTokensAsync())
            await deviceTokenApi.RevokeDeviceTokenAsync(device.Id);
        MascotLink.Remove();
    }

    // Al cerrar sesión, la próxima sesión vuelve a verificar.
    public void ResetSessionCheck() => _checkedThisSession = false;
}
