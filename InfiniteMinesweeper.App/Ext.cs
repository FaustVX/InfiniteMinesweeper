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
