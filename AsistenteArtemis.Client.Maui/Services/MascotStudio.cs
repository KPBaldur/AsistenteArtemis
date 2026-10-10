using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AsistenteArtemis.Client.Maui.Services;

// Configuración de animaciones de la mascota (DesktopMascot) desde la app. Contrato con
// DesktopMascot/main.gd:
// - De fábrica (repo): DesktopMascot/animations/*.vrma|fbx, avatars/<id>/avatar.json y el
//   catálogo avatars/categories.json.
// - Del usuario (user:// de Godot = %APPDATA%\Godot\app_userdata\DesktopMascot): un
//   avatars/<id>/avatar.json que reemplaza al de fábrica, y animations/ con lo importado.
// - command.json ({"id": n, "play": "<animación>"}) para que la mascota reproduzca algo.
// La mascota revisa esos archivos cada segundo y se actualiza sola.
public static partial class MascotStudio
{
    public const string DefaultAvatar = "muestra";
    private static readonly string[] SupportedExtensions = [".vrma", ".fbx"];

    public record Category(string Id, string Label, string Trigger, bool IsCustom);
    public record AnimationFile(string Name, string Extension, bool IsImported);

    public class Profile
    {
        public string AvatarId { get; init; } = DefaultAvatar;
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, List<string>> Animations { get; init; } = [];
        public Dictionary<string, string> CustomCategories { get; init; } = [];
        internal JsonObject Raw { get; init; } = new();
    }

    private static string UserDir => MascotLauncher.UserDataDir;
    private static string UserAnimationsDir => Path.Combine(UserDir, "animations");

    public static string? ProjectDir => MascotLauncher.FindProjectDir();

    // [avatar] id de mascot.cfg (formato ConfigFile de Godot), o el de fábrica.
    public static string ActiveAvatarId()
    {
        var cfg = Path.Combine(UserDir, "mascot.cfg");
        if (!File.Exists(cfg))
            return DefaultAvatar;
        var inAvatar = false;
        foreach (var line in File.ReadLines(cfg))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('['))
                inAvatar = trimmed == "[avatar]";
            else if (inAvatar && trimmed.StartsWith("id="))
                return trimmed[3..].Trim('"');
        }
        return DefaultAvatar;
    }

    public static List<Category> LoadCategories(Profile profile)
    {
        var list = new List<Category>();
        var catalog = ProjectDir is { } dir ? Path.Combine(dir, "avatars", "categories.json") : null;
        if (catalog is not null && File.Exists(catalog))
        {
            foreach (var node in JsonNode.Parse(File.ReadAllText(catalog))!.AsArray())
            {
                list.Add(new Category((string)node!["id"]!, (string)node["label"]!,
                    (string?)node["trigger"] ?? "auto", IsCustom: false));
            }
        }
        foreach (var (id, label) in profile.CustomCategories)
        {
            if (list.All(c => c.Id != id))
                list.Add(new Category(id, label, "manual", IsCustom: true));
        }
        return list;
    }

    public static Profile LoadProfile(string? avatarId = null)
    {
        avatarId ??= ActiveAvatarId();
        var userPath = Path.Combine(UserDir, "avatars", avatarId, "avatar.json");
        var factoryPath = ProjectDir is { } dir ? Path.Combine(dir, "avatars", avatarId, "avatar.json") : null;
        var path = File.Exists(userPath) ? userPath : factoryPath;

        var raw = path is not null && File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? new JsonObject()
            : new JsonObject();

        var profile = new Profile { AvatarId = avatarId, Name = (string?)raw["name"] ?? avatarId, Raw = raw };
        if (raw["animations"] is JsonObject animations)
        {
            foreach (var (category, names) in animations)
                profile.Animations[category] = names?.AsArray().Select(n => (string)n!).ToList() ?? [];
        }
        if (raw["categories"] is JsonObject custom)
        {
            foreach (var (id, label) in custom)
                profile.CustomCategories[id] = (string?)label ?? id;
        }
        return profile;
    }

    // Siempre en user://: el de fábrica (repo) no se toca. Se conservan los demás campos del
    // perfil (model, etc.) tal como estaban.
    public static void SaveProfile(Profile profile)
    {
        var raw = profile.Raw.DeepClone().AsObject();
        raw["name"] = profile.Name;
        raw["animations"] = new JsonObject(profile.Animations.Select(kv =>
            KeyValuePair.Create(kv.Key, (JsonNode?)new JsonArray(kv.Value.Select(n => (JsonNode?)n).ToArray()))));
        raw["categories"] = new JsonObject(profile.CustomCategories.Select(kv =>
            KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)));

        var dir = Path.Combine(UserDir, "avatars", profile.AvatarId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "avatar.json"),
            raw.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static List<AnimationFile> ListAnimations()
    {
        var files = new Dictionary<string, AnimationFile>(StringComparer.OrdinalIgnoreCase);
        void Scan(string? dir, bool imported)
        {
            if (dir is null || !Directory.Exists(dir))
                return;
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (SupportedExtensions.Contains(ext))
                    files[Path.GetFileNameWithoutExtension(file)] = new AnimationFile(Path.GetFileNameWithoutExtension(file), ext, imported);
            }
        }
        Scan(ProjectDir is { } project ? Path.Combine(project, "animations") : null, imported: false);
        Scan(UserAnimationsDir, imported: true);
        return files.Values.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <returns>El nombre con que quedó (sin extensión).</returns>
    public static string ImportAnimation(string sourcePath, string? displayName = null)
    {
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (!SupportedExtensions.Contains(ext))
            throw new InvalidOperationException("Solo se aceptan animaciones .vrma o .fbx (Mixamo).");

        var name = SafeName(displayName ?? Path.GetFileNameWithoutExtension(sourcePath));
        Directory.CreateDirectory(UserAnimationsDir);
        File.Copy(sourcePath, Path.Combine(UserAnimationsDir, name + ext), overwrite: true);
        return name;
    }

    public static void DeleteImported(AnimationFile file)
    {
        if (!file.IsImported)
            throw new InvalidOperationException("Las animaciones de fábrica no se borran desde la app.");
        var path = Path.Combine(UserAnimationsDir, file.Name + file.Extension);
        if (File.Exists(path))
            File.Delete(path);
    }

    // Pide a la mascota (si está abierta) que reproduzca una animación.
    public static void PlayOnMascot(string animationName)
    {
        Directory.CreateDirectory(UserDir);
        var command = new JsonObject { ["id"] = DateTime.UtcNow.Ticks, ["play"] = animationName };
        File.WriteAllText(Path.Combine(UserDir, "command.json"), command.ToJsonString());
    }

    // Identificador de categoría a partir de su nombre: "Saludo matutino" -> "saludo_matutino".
    public static string CategoryId(string label)
    {
        var id = SafeName(label).ToLowerInvariant();
        return string.IsNullOrEmpty(id) ? "categoria" : id;
    }

    private static string SafeName(string name)
    {
        var normalized = name.Normalize(System.Text.NormalizationForm.FormD);
        var ascii = new string(normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
            != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        return UnsafeChars().Replace(ascii.Trim(), "_").Trim('_');
    }

    [GeneratedRegex(@"[^A-Za-z0-9_\-]+")]
    private static partial Regex UnsafeChars();
}
