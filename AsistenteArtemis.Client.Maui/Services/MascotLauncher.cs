using System.Diagnostics;

namespace AsistenteArtemis.Client.Maui.Services;

// Abre la mascota de escritorio (DesktopMascot, Godot) desde la app. Solo Windows: es un
// proceso aparte, y en Android no hay mascota.
//
// - Motor: $env:GODOT_EXE o Documents\Tools\godot-engine (igual que run-mascot.ps1).
// - Proyecto: $env:ARTEMIS_MASCOT_PATH o, en desarrollo, la carpeta DesktopMascot del repo,
//   que se busca subiendo desde el ejecutable de la app (bin\...\ dentro del repo).
// - "¿Ya está abierta?": la mascota escribe su PID en user://mascot.pid al arrancar.
public static class MascotLauncher
{
    private const string GodotFileName = "Godot_v4.7.2-stable_win64.exe";

    private static string UserDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Godot", "app_userdata", "DesktopMascot");

    public static bool IsSupported => OperatingSystem.IsWindows();

    // Último motivo por el que no se pudo abrir; Identidad lo muestra (la barra lateral no
    // tiene dónde).
    public static string? LastError { get; private set; }

    public static bool IsRunning()
    {
        try
        {
            var pidFile = Path.Combine(UserDataDir, "mascot.pid");
            if (!File.Exists(pidFile) || !int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid))
                return false;
            using var process = Process.GetProcessById(pid);
            return !process.HasExited && process.ProcessName.StartsWith("Godot", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false; // el PID ya no existe: quedó un archivo viejo de una mascota cerrada
        }
    }

    /// <returns>null si la abrió (o ya estaba abierta); si no, el motivo para mostrarle al usuario.</returns>
    public static string? Launch() => LastError = TryLaunch();

    private static string? TryLaunch()
    {
        if (!IsSupported)
            return "La mascota solo existe en Windows.";
        if (IsRunning())
            return null;

        var godot = Environment.GetEnvironmentVariable("GODOT_EXE");
        if (string.IsNullOrWhiteSpace(godot))
            godot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents", "Tools", "godot-engine", GodotFileName);
        if (!File.Exists(godot))
            return $"No encontré Godot en {godot}. Define la variable GODOT_EXE con su ruta.";

        var project = FindProjectDir();
        if (project is null)
            return "No encontré la carpeta DesktopMascot. Define la variable ARTEMIS_MASCOT_PATH con su ruta.";

        Process.Start(new ProcessStartInfo
        {
            FileName = godot,
            Arguments = $"--path \"{project}\"",
            UseShellExecute = false,
            WorkingDirectory = project,
        });
        return null;
    }

    private static string? FindProjectDir()
    {
        var fromEnv = Environment.GetEnvironmentVariable("ARTEMIS_MASCOT_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(Path.Combine(fromEnv, "project.godot")))
            return fromEnv;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "DesktopMascot");
            if (File.Exists(Path.Combine(candidate, "project.godot")))
                return candidate;
        }
        return null;
    }
}
