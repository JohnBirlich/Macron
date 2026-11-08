namespace Macron.App;

using System.Text.Json;

/// <summary>
/// Чтение/запись настроек в %APPDATA%\Macron\settings.json.
/// Настройки не содержат ввода пользователя — только технические параметры.
/// </summary>
public static class SettingsService
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Macron");

    private static readonly string Path = Path.Combine(Folder, "settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    static SettingsService()
    {
        Directory.CreateDirectory(Folder);
    }

    public static Settings Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path), Options);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // Повреждённый файл настроек не роняет приложение.
        }

        return new Settings();
    }

    public static void Save(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var temp = Path.Combine(Folder, $"{Path.GetRandomFileName()}.tmp");
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, Path, overwrite: true);
    }
}
