namespace Macron.Core;

using System.IO;

/// <summary>
/// Чтение и атомарная запись файла макроса.
/// Сохранение: временный файл → замена (старый файл уходит в .bak),
/// так что при ошибке диска исходный файл не затирается.
/// </summary>
public static class MacroFileStorage
{
    public static void Save(string path, Macro macro)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = Path.Combine(directory ?? "", $"{Path.GetRandomFileName()}.tmp");
        var bak = path + ".bak";

        try
        {
            File.WriteAllText(temp, MacroSerializer.Serialize(macro));

            if (File.Exists(path))
            {
                if (File.Exists(bak))
                {
                    File.Delete(bak);
                }

                File.Move(path, bak);
            }

            File.Move(temp, path);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch
                {
                    // Игнорируем: основная операция уже завершилась (или упала).
                }
            }
        }
    }

    public static Macro Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = File.ReadAllText(path);
        return MacroSerializer.Deserialize(json);
    }
}
