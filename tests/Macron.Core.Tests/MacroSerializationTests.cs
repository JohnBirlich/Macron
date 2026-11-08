namespace Macron.Core.Tests;

public class MacroSerializationTests
{
    [Fact]
    public void Round_trip_preserves_all_event_types()
    {
        var macro = new Macro
        {
            Name = "Заполнить форму",
            Events = new List<MacronEvent>
            {
                new MouseMoveEvent { DtMs = 0, X = 640, Y = 420 },
                new MouseDownEvent { DtMs = 85, Button = MouseButton.Left },
                new MouseUpEvent { DtMs = 62, Button = MouseButton.Left },
                new MouseWheelEvent { DtMs = 100, Delta = 120 },
                new KeyDownEvent { DtMs = 310, Vk = 65, Scan = 30 },
                new KeyUpEvent { DtMs = 74, Vk = 65, Scan = 30 }
            }
        };

        var json = MacroSerializer.Serialize(macro);
        var back = MacroSerializer.Deserialize(json);

        Assert.Equal(1, back.FormatVersion);
        Assert.Equal("Заполнить форму", back.Name);
        Assert.Equal(6, back.EventCount);
        Assert.Equal(macro.TotalDurationMs, back.TotalDurationMs);

        var e0 = Assert.IsType<MouseMoveEvent>(back.Events[0]);
        Assert.Equal(640, e0.X);
        Assert.Equal(420, e0.Y);
        Assert.Equal(0, e0.DtMs);

        var e1 = Assert.IsType<MouseDownEvent>(back.Events[1]);
        Assert.Equal(MouseButton.Left, e1.Button);
        Assert.Equal(85, e1.DtMs);

        var e4 = Assert.IsType<KeyDownEvent>(back.Events[4]);
        Assert.Equal(65, e4.Vk);
        Assert.Equal(30, e4.Scan);

        var e5 = Assert.IsType<KeyUpEvent>(back.Events[5]);
        Assert.Equal(74, e5.DtMs);
    }

    [Fact]
    public void Json_uses_camel_case_and_type_discriminator()
    {
        var macro = new Macro { Events = new List<MacronEvent> { new KeyDownEvent { DtMs = 5, Vk = 65, Scan = 30 } } };
        var json = MacroSerializer.Serialize(macro);

        Assert.Contains("\"dtMs\"", json);
        Assert.Contains("\"type\"", json);
        Assert.Contains("\"KeyDown\"", json);
        Assert.Contains("\"formatVersion\"", json);
        Assert.Contains("\"createdUtc\"", json);
    }

    [Fact]
    public void Invalid_json_throws_instead_of_crashing()
    {
        Assert.Throws<JsonException>(() => MacroSerializer.Deserialize("{ not valid json"));
        Assert.Throws<JsonException>(() => MacroSerializer.Deserialize(""));
        Assert.Throws<JsonException>(() => MacroSerializer.Deserialize("{\"formatVersion\":1,\"events\":[]}"));
    }

    [Fact]
    public void Unknown_event_type_throws()
    {
        var json = """
            {
              "formatVersion": 1,
              "name": "x",
              "events": [
                { "dtMs": 0, "type": "NotARealType" }
              ]
            }
            """;
        Assert.Throws<JsonException>(() => MacroSerializer.Deserialize(json));
    }
}
