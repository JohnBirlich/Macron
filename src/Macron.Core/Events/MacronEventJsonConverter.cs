namespace Macron.Core.Events;

using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class MacronEventJsonConverter : JsonConverter<MacronEvent>
{
    public override MacronEvent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Ожидался объект события.");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        long dtMs = root.TryGetProperty("dtMs", out var dt) ? dt.GetInt64() : 0;
        string type = root.GetProperty("type").GetString()
            ?? throw new JsonException("Свойство 'type' обязательно.");

        return type switch
        {
            "MouseMove" => Deserialize<MouseMoveEvent>(root, options) with { DtMs = dtMs },
            "MouseDown" => Deserialize<MouseDownEvent>(root, options) with { DtMs = dtMs },
            "MouseUp" => Deserialize<MouseUpEvent>(root, options) with { DtMs = dtMs },
            "MouseWheel" => Deserialize<MouseWheelEvent>(root, options) with { DtMs = dtMs },
            "KeyDown" => Deserialize<KeyDownEvent>(root, options) with { DtMs = dtMs },
            "KeyUp" => Deserialize<KeyUpEvent>(root, options) with { DtMs = dtMs },
            _ => throw new JsonException($"Неизвестный тип события: {type}")
        };
    }

    private static T Deserialize<T>(JsonElement element, JsonSerializerOptions options)
        where T : MacronEvent
    {
        return element.Deserialize<T>(options)
            ?? throw new JsonException($"Не удалось десериализовать {typeof(T).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, MacronEvent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("dtMs", value.DtMs);

        switch (value)
        {
            case MouseMoveEvent m:
                writer.WriteString("type", "MouseMove");
                writer.WriteNumber("x", m.X);
                writer.WriteNumber("y", m.Y);
                break;
            case MouseDownEvent m:
                writer.WriteString("type", "MouseDown");
                writer.WriteString("button", m.Button.ToString());
                break;
            case MouseUpEvent m:
                writer.WriteString("type", "MouseUp");
                writer.WriteString("button", m.Button.ToString());
                break;
            case MouseWheelEvent m:
                writer.WriteString("type", "MouseWheel");
                writer.WriteNumber("delta", m.Delta);
                break;
            case KeyDownEvent k:
                writer.WriteString("type", "KeyDown");
                writer.WriteNumber("vk", k.Vk);
                writer.WriteNumber("scan", k.Scan);
                break;
            case KeyUpEvent k:
                writer.WriteString("type", "KeyUp");
                writer.WriteNumber("vk", k.Vk);
                writer.WriteNumber("scan", k.Scan);
                break;
            default:
                throw new JsonException($"Неизвестный тип события: {value.GetType()}");
        }

        writer.WriteEndObject();
    }
}
