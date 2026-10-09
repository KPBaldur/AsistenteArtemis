namespace AsistenteArtemis.Client.Maui.Services;

// Conexión con la mascota de escritorio (DesktopMascot, Godot): la app le deja la URL de la
// Api y un token de dispositivo en el directorio de datos de Godot ("user://" del proyecto),
// en un archivo propio (api.cfg) distinto de mascot.cfg, que la mascota reescribe cada vez
// que se la arrastra y pisaría el token.
public static class MascotLink
{
    public const string DeviceName = "Mascota de escritorio";

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Godot", "app_userdata", "DesktopMascot", "api.cfg");

    public static bool IsLinked => File.Exists(ConfigPath);

    // Formato ConfigFile de Godot. El token es base64url y la URL no lleva comillas, así que
    // no hace falta escapar nada.
    public static void Write(string token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath,
            $"[api]\n\nbase_url=\"{ApiConfig.BaseUrl}\"\ntoken=\"{token}\"\n");
    }

    public static void Remove()
    {
        if (File.Exists(ConfigPath))
            File.Delete(ConfigPath);
    }
}
