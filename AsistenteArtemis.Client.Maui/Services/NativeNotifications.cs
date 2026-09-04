namespace AsistenteArtemis.Client.Maui.Services;

// Compartido entre Alarms.razor (banner en pantalla) y AlarmNotificationService (toast nativo
// de Windows desde cualquier página) — un solo lugar para el texto del aviso y para disparar el
// toast, no duplicado en los dos. Sin "using" a nivel de archivo para el paquete de
// notificaciones de Windows (rompería la compilación de Android, que no lo tiene) — tipo
// calificado completo adentro del método guardado por #if WINDOWS.
public static class NativeNotifications
{
    public static string BuildRingingMessage(string alarmName, bool hasActiveBreak, string? phaseJustEnded) =>
        hasActiveBreak switch
        {
            true when phaseJustEnded == "Work" => $"¡Terminó tu tiempo de trabajo en \"{alarmName}\"! Descansando...",
            true => $"¡Terminó tu descanso en \"{alarmName}\"! Volviendo a trabajar...",
            _ => $"¡Sonó la alarma \"{alarmName}\"!"
        };

    // Best-effort: si falla (paquete no inicializado, permiso denegado, etc.) no debe romper
    // nada de lo que ya funciona (banner, audio) — mismo criterio que el resto de la app.
    public static void ShowToast(string message)
    {
#if WINDOWS
        try
        {
            new CommunityToolkit.WinUI.Notifications.ToastContentBuilder()
                .AddText("AsistenteArtemis")
                .AddText(message)
                .Show();
        }
        catch
        {
        }
#endif
    }
}
