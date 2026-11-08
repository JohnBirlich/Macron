namespace Macron.Core;

using System.Text.Json;
using Macron.Core.Events;

/// <summary>
/// Сериализация макроса в формат .macron.json (и обратно).
/// Имена свойств — camelCase, как в спецификации плана разработки.
/// </summary>
public static class MacroSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    static MacroSerializer()
    {
        Options.Converters.Add(new MacronEventJsonConverter());
    }

    public static string Serialize(Macro macro)
    {
        return JsonSerializer.Serialize(macro, Options);
    }

    public static Macro Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("Пустой JSON.");
        }

        return JsonSerializer.Deserialize<Macro>(json, Options)
            ?? throw new JsonException("Не удалось десериализовать макрос.");
    }
}
