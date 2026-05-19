using System.Text.Json;
using System.Text.Json.Serialization;

namespace Web.Api.Infrastructure;

// Forces JSON DateTime to UTC kind - Npgsql refuses Unspecified for timestamp with time zone.
internal sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
  public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    DateTime value = reader.GetDateTime();
    return value.Kind switch
    {
      DateTimeKind.Utc => value,
      DateTimeKind.Local => value.ToUniversalTime(),
      _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
  }

  public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
  {
    DateTime utc = value.Kind switch
    {
      DateTimeKind.Utc => value,
      DateTimeKind.Local => value.ToUniversalTime(),
      _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
    writer.WriteStringValue(utc);
  }
}

internal sealed class NullableUtcDateTimeJsonConverter : JsonConverter<DateTime?>
{
  public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType == JsonTokenType.Null)
    {
      return null;
    }

    DateTime value = reader.GetDateTime();
    return value.Kind switch
    {
      DateTimeKind.Utc => value,
      DateTimeKind.Local => value.ToUniversalTime(),
      _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
  }

  public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
  {
    if (value is null)
    {
      writer.WriteNullValue();
      return;
    }

    DateTime utc = value.Value.Kind switch
    {
      DateTimeKind.Utc => value.Value,
      DateTimeKind.Local => value.Value.ToUniversalTime(),
      _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
    };
    writer.WriteStringValue(utc);
  }
}
