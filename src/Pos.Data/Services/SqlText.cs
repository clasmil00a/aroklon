using System.Globalization;
using System.Text.Json;

namespace Pos.Data.Services;

public static class SqlText
{
    public static string Now(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static DateTimeOffset? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    public static string? DisplaySetting(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.String => document.RootElement.GetString(),
            JsonValueKind.Null => null,
            _ => document.RootElement.GetRawText()
        };
    }

    public static string Serialize(object? value) => JsonSerializer.Serialize(value);
}
