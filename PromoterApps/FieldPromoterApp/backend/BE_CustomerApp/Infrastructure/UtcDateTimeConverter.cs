using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BE_CustomerApp.Infrastructure;

/// <summary>
/// Every DateTime in the database is UTC, but Dapper reads DATETIME2 as Kind=Unspecified, which System.Text.Json
/// writes without a zone ("2026-11-06T14:25:00"). Clients then read it as their own local time and show it 3 hours
/// early in Iraq. This writes all DateTimes as UTC with a trailing "Z", and reads incoming dates as UTC.
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Utc => value,
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture));
    }
}
