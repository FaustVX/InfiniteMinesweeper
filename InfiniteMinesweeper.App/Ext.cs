using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace InfiniteMinesweeper;

public static class Ext
{
    extension(ref Utf8JsonReader reader)
    {
        public bool TryGetProperty([NotNullWhen(true)] out string? propertyName)
        {
            if (!reader.Read())
            {
                propertyName = default;
                return false;
            }

            if (reader.TokenType == JsonTokenType.EndObject)
            {
                propertyName = default;
                return false;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                propertyName = default;
                return false;
            }

            propertyName = reader.GetString()!;
            return reader.Read();
        }
    }

    extension(Utf8JsonWriter writer)
    {
        public CloseObjectDisposable StartObject(JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            return new(writer, options);
        }

        public CloseArrayDisposable StartArray(JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            return new(writer, options);
        }
    }

    public readonly ref struct CloseObjectDisposable(Utf8JsonWriter writer, JsonSerializerOptions options) : IDisposable
    {
        public void Dispose()
        => writer.WriteEndObject();

        public void WriteProperty<T>(ReadOnlySpan<char> property, T value)
        {
            writer.WritePropertyName(property);
            JsonSerializer.Serialize(writer, value, options);
        }

        public void WritePropertyArray<T>(ReadOnlySpan<char> property, IEnumerable<T> value)
        {
            writer.WritePropertyName(property);
            using var a = writer.StartArray(options);
            foreach (var item in value)
                JsonSerializer.Serialize(writer, item, options);
        }

        public CloseArrayDisposable StartArray(ReadOnlySpan<char> property)
        {
            writer.WritePropertyName(property);
            writer.WriteStartArray();
            return new(writer, options);
        }
    }

    public readonly ref struct CloseArrayDisposable(Utf8JsonWriter writer, JsonSerializerOptions options) : IDisposable
    {
        public void WriteArray<T>(IEnumerable<T> value)
        {
            foreach (var item in value)
                JsonSerializer.Serialize(writer, item, options);
        }

        public void Dispose()
        => writer.WriteEndArray();
    }
}

public static class ExtClass
{
    extension(ref Utf8JsonReader reader)
    {
        public bool TryGetProperty<T>(JsonSerializerOptions options, [NotNullWhen(true)]out string? propertyName, [NotNullWhen(true)]out T? value)
        where T : class
        {
            if (!reader.TryGetProperty(out propertyName))
            {
                (propertyName, value) = (default, default);
                return false;
            }
            value = JsonSerializer.Deserialize<T>(ref reader, options);
            return value is not null;
        }
    }

    extension(JsonSerializer)
    {
        /// <inheritdoc cref="JsonSerializer.Deserialize{TValue}(ref Utf8JsonReader, JsonSerializerOptions?)"/>
        public static TValue? TryDeserialize<TValue>(ref Utf8JsonReader reader, JsonSerializerOptions options)
        where TValue : class
        {
            try
            {
                return JsonSerializer.Deserialize<TValue>(ref reader, options);
            }
            catch (JsonException)
            {
                return default;
            }
        }
    }
}

public static class ExtStruct
{
    extension(ref Utf8JsonReader reader)
    {
        public bool TryGetProperty<T>(JsonSerializerOptions options, [NotNullWhen(true)]out string? propertyName, [NotNullWhen(true)]out T? value)
        where T : struct
        {
            if (!reader.TryGetProperty(out propertyName))
            {
                (propertyName, value) = (default, default);
                return false;
            }
            value = JsonSerializer.Deserialize<T>(ref reader, options);
            return true;
        }
    }
}
