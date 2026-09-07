namespace AsistenteArtemis.Client.Maui.Services;

// Texto del aviso de "sonó la alarma", compartido entre el popup propio de la app
// (Components/Shared/AlarmPopup.razor) y AlarmNotificationService — un solo lugar para esta
// lógica, no duplicada. El toast nativo de Windows (CommunityToolkit.WinUI.Notifications) que
// vivía acá se descartó por intermitente — ver decisión del 2026-09-04 en el wiki.
public static class NativeNotifications
{
    public static string BuildRingingMessage(string alarmName, bool hasActiveBreak, string? phaseJustEnded) =>
        hasActiveBreak switch
        {
            true when phaseJustEnded == "Work" => $"¡Terminó tu tiempo de trabajo en \"{alarmName}\"! Descansando...",
            true => $"¡Terminó tu descanso en \"{alarmName}\"! Volviendo a trabajar...",
            _ => $"¡Sonó la alarma \"{alarmName}\"!"
        };
}
