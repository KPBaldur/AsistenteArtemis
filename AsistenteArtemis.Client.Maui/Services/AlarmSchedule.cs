using AsistenteArtemis.Client.Maui.Models.Dtos;
using AsistenteArtemis.Client.Maui.Models.Enums;

namespace AsistenteArtemis.Client.Maui.Services;

// Hora REAL en que va a sonar una alarma, compartida por "Hoy" y "Próximas alarmas".
public static class AlarmSchedule
{
    private static readonly (DayOfWeek Day, AlarmDaysOfWeek Flag)[] DayMap =
    [
        (DayOfWeek.Monday, AlarmDaysOfWeek.Monday), (DayOfWeek.Tuesday, AlarmDaysOfWeek.Tuesday),
        (DayOfWeek.Wednesday, AlarmDaysOfWeek.Wednesday), (DayOfWeek.Thursday, AlarmDaysOfWeek.Thursday),
        (DayOfWeek.Friday, AlarmDaysOfWeek.Friday), (DayOfWeek.Saturday, AlarmDaysOfWeek.Saturday),
        (DayOfWeek.Sunday, AlarmDaysOfWeek.Sunday)
    ];

    // Sin ventana configurada, ScheduleAt ya es el próximo horario real. Con ventana, el
    // scheduler puede dejar ScheduleAt en un momento fuera de la ventana mientras reintenta
    // en silencio (ver AlarmSchedulerService.IsWithinSchedule del lado servidor) — para mostrar
    // la hora REAL en que va a sonar, buscamos la próxima ocurrencia válida acá, una sola vez
    // al cargar (nada de recalcular en un timer).
    public static DateTime ComputeNextRing(AlarmDto alarm)
    {
        var scheduleAtLocal = alarm.ScheduleAt!.Value.ToLocalTime();
        if (!alarm.WindowStart.HasValue || !alarm.WindowEnd.HasValue)
            return scheduleAtLocal;

        var candidate = scheduleAtLocal > DateTime.Now ? scheduleAtLocal : DateTime.Now;
        for (var d = 0; d < 8; d++)
        {
            var day = candidate.Date.AddDays(d);
            var flag = DayMap.First(m => m.Day == day.DayOfWeek).Flag;
            if (!alarm.DaysOfWeek.HasFlag(flag)) continue;

            var windowStart = day.Add(alarm.WindowStart.Value.ToTimeSpan());
            var windowEnd = day.Add(alarm.WindowEnd.Value.ToTimeSpan());
            var effectiveStart = d == 0 && candidate > windowStart ? candidate : windowStart;
            if (effectiveStart <= windowEnd)
                return effectiveStart;
        }

        return scheduleAtLocal; // Configuración inconsistente (ej. ningún día marcado) — mostrar el valor crudo.
    }
}
