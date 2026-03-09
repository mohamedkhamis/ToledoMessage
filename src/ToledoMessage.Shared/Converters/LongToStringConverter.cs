using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ToledoMessage.Shared.Converters;

/// <summary>
/// Serializes long values as JSON strings to avoid JavaScript precision loss
/// for IDs exceeding Number.MAX_SAFE_INTEGER (2^53 - 1).
/// </summary>
public sealed class LongToStringConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => long.Parse(reader.GetString()!, CultureInfo.InvariantCulture),
            JsonTokenType.Number => reader.GetInt64(),
            _ => throw new JsonException($"Unexpected token type {reader.TokenType} for long")
        };
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Serializes nullable long values as JSON strings.
/// </summary>
public sealed class LongNullableToStringConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        return reader.TokenType switch
        {
            JsonTokenType.String => long.Parse(reader.GetString()!, CultureInfo.InvariantCulture),
            JsonTokenType.Number => reader.GetInt64(),
            _ => throw new JsonException($"Unexpected token type {reader.TokenType} for long?")
        };
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
    }
}
