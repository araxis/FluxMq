using System.Reflection;
using System.Text;
using System.Text.Json;

namespace FluxMq.Components.Mapping;

internal static class ExpressionObjectReader
{
    public static string ReadRequiredString(object source, string propertyName)
        => ReadOptionalString(source, propertyName) ??
            throw new InvalidOperationException($"Mapped object requires property '{propertyName}'.");

    public static string? ReadOptionalString(object source, string propertyName)
    {
        if (!TryRead(source, propertyName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => text,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            JsonElement element when element.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            _ => value.ToString()
        };
    }

    public static bool ReadBoolOrDefault(object source, string propertyName, bool fallback)
    {
        if (!TryRead(source, propertyName, out var value) || value is null)
        {
            return fallback;
        }

        return value switch
        {
            bool boolean => boolean,
            string text when bool.TryParse(text, out var boolean) => boolean,
            JsonElement element when element.ValueKind == JsonValueKind.True => true,
            JsonElement element when element.ValueKind == JsonValueKind.False => false,
            JsonElement element when element.ValueKind == JsonValueKind.String &&
                                     bool.TryParse(element.GetString(), out var boolean) => boolean,
            _ => throw new InvalidOperationException($"Mapped object property '{propertyName}' must be a boolean.")
        };
    }

    public static byte[] ReadBytesOrDefault(object source, string propertyName, byte[] fallback)
    {
        if (!TryRead(source, propertyName, out var value) || value is null)
        {
            return fallback;
        }

        return value switch
        {
            byte[] bytes => bytes,
            string text => Encoding.UTF8.GetBytes(text),
            JsonElement element => ReadBytes(element, propertyName),
            _ => throw new InvalidOperationException(
                $"Mapped object property '{propertyName}' must be a string, byte array, JSON object, or JSON array.")
        };
    }

    public static byte[]? ReadOptionalBytes(object source, string propertyName)
    {
        if (!TryRead(source, propertyName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => bytes,
            string text => Encoding.UTF8.GetBytes(text),
            JsonElement element => ReadBytes(element, propertyName),
            _ => throw new InvalidOperationException(
                $"Mapped object property '{propertyName}' must be a string, byte array, JSON object, or JSON array.")
        };
    }

    public static int? ReadOptionalInt(object source, string propertyName)
    {
        if (!TryRead(source, propertyName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int number => number,
            long number => checked((int)number),
            string text when int.TryParse(text, out var number) => number,
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number) => number,
            JsonElement element when element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var number) => number,
            _ => throw new InvalidOperationException($"Mapped object property '{propertyName}' must be an integer.")
        };
    }

    public static Dictionary<string, string> ReadStringDictionaryOrEmpty(object source, string propertyName)
    {
        if (!TryRead(source, propertyName, out var value) || value is null)
        {
            return [];
        }

        if (value is Dictionary<string, string> dictionary)
        {
            return new Dictionary<string, string>(dictionary, StringComparer.OrdinalIgnoreCase);
        }

        if (value is IReadOnlyDictionary<string, string> readOnlyDictionary)
        {
            return new Dictionary<string, string>(readOnlyDictionary, StringComparer.OrdinalIgnoreCase);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } element)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.GetRawText();
            }

            return result;
        }

        throw new InvalidOperationException($"Mapped object property '{propertyName}' must be an object with string values.");
    }

    public static TEnum ReadEnumOrDefault<TEnum>(object source, string propertyName, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (!TryRead(source, propertyName, out var value) || value is null)
        {
            return fallback;
        }

        return value switch
        {
            TEnum enumValue => enumValue,
            int number => ParseEnum<TEnum>(number, propertyName),
            long number => ParseEnum<TEnum>(checked((int)number), propertyName),
            string text => ParseEnum<TEnum>(text, propertyName),
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number)
                => ParseEnum<TEnum>(number, propertyName),
            JsonElement element when element.ValueKind == JsonValueKind.String
                => ParseEnum<TEnum>(element.GetString() ?? string.Empty, propertyName),
            _ => throw new InvalidOperationException(
                $"Mapped object property '{propertyName}' must be a {typeof(TEnum).Name} value, number, or string.")
        };
    }

    public static Guid ReadRequiredGuid(object source, string propertyName)
    {
        if (!TryRead(source, propertyName, out var value) || value is null)
        {
            throw new InvalidOperationException($"Mapped object requires property '{propertyName}'.");
        }

        return value switch
        {
            Guid guid => guid,
            string text when Guid.TryParse(text, out var guid) => guid,
            JsonElement element when element.ValueKind == JsonValueKind.String &&
                                     Guid.TryParse(element.GetString(), out var guid) => guid,
            _ => throw new InvalidOperationException($"Mapped object property '{propertyName}' must be a GUID.")
        };
    }

    public static bool TryRead(object source, string propertyName, out object? value)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (source is JsonElement element)
        {
            if (TryGetJsonProperty(element, propertyName, out var property))
            {
                value = property;
                return true;
            }

            value = null;
            return false;
        }

        var propertyInfo = source.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (propertyInfo is not null)
        {
            value = propertyInfo.GetValue(source);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetJsonProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            property = default;
            return false;
        }

        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value.Clone();
                return true;
            }
        }

        property = default;
        return false;
    }

    private static byte[] ReadBytes(JsonElement element, string propertyName)
        => element.ValueKind switch
        {
            JsonValueKind.String => Encoding.UTF8.GetBytes(element.GetString() ?? string.Empty),
            JsonValueKind.Array when TryReadByteArray(element, out var bytes) => bytes,
            JsonValueKind.Array or JsonValueKind.Object => JsonSerializer.SerializeToUtf8Bytes(element),
            JsonValueKind.Null => [],
            _ => Encoding.UTF8.GetBytes(element.GetRawText())
        };

    private static bool TryReadByteArray(JsonElement element, out byte[] bytes)
    {
        var result = new List<byte>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetByte(out var value))
            {
                bytes = [];
                return false;
            }

            result.Add(value);
        }

        bytes = [.. result];
        return true;
    }

    private static TEnum ParseEnum<TEnum>(int value, string propertyName)
        where TEnum : struct, Enum
    {
        if (Enum.IsDefined(typeof(TEnum), value))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), value);
        }

        throw new InvalidOperationException($"Mapped object property '{propertyName}' has an unsupported {typeof(TEnum).Name} value.");
    }

    private static TEnum ParseEnum<TEnum>(string value, string propertyName)
        where TEnum : struct, Enum
    {
        if (int.TryParse(value, out var number))
        {
            return ParseEnum<TEnum>(number, propertyName);
        }

        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var enumValue))
        {
            return enumValue;
        }

        throw new InvalidOperationException($"Mapped object property '{propertyName}' has an unsupported {typeof(TEnum).Name} value.");
    }
}
