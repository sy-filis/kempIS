using System.Text.Json;

namespace Web.Api.Realtime;

internal sealed record RealtimeEnvelope(string Event, JsonElement Data)
{
  // Not disposed: disposal returns the ArrayPool buffer and invalidates handed-out JsonElement views.
  private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement;
  private static readonly JsonWriterOptions WriterOptions = new() { Indented = false };

  public static string Serialize(RealtimeEnvelope envelope)
  {
    using var ms = new MemoryStream();
    using (var writer = new Utf8JsonWriter(ms, WriterOptions))
    {
      writer.WriteStartObject();
      writer.WriteString("event", envelope.Event);
      writer.WritePropertyName("data");
      envelope.Data.WriteTo(writer);
      writer.WriteEndObject();
    }

    return System.Text.Encoding.UTF8.GetString(ms.ToArray());
  }

  public static string SerializeFromObject(string eventName, object? payload)
  {
    using var ms = new MemoryStream();
    using (var writer = new Utf8JsonWriter(ms, WriterOptions))
    {
      writer.WriteStartObject();
      writer.WriteString("event", eventName);
      writer.WritePropertyName("data");
      if (payload is null)
      {
        writer.WriteStartObject();
        writer.WriteEndObject();
      }
      else
      {
        JsonSerializer.Serialize(writer, payload);
      }

      writer.WriteEndObject();
    }

    return System.Text.Encoding.UTF8.GetString(ms.ToArray());
  }

  public static RealtimeEnvelope? TryParse(string json)
  {
    try
    {
      using var doc = JsonDocument.Parse(json);
      if (doc.RootElement.ValueKind != JsonValueKind.Object)
      {
        return null;
      }

      if (!doc.RootElement.TryGetProperty("event", out JsonElement eventProp) ||
          eventProp.ValueKind != JsonValueKind.String)
      {
        return null;
      }

      string? eventName = eventProp.GetString();
      if (string.IsNullOrEmpty(eventName))
      {
        return null;
      }

      JsonElement data = EmptyObject;
      if (doc.RootElement.TryGetProperty("data", out JsonElement dataProp) &&
          dataProp.ValueKind == JsonValueKind.Object)
      {
        data = dataProp.Clone();
      }

      return new RealtimeEnvelope(eventName, data);
    }
    catch (JsonException)
    {
      return null;
    }
  }
}
