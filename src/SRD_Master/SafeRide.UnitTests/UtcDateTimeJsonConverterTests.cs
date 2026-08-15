using System.Text.Json;
using SafeRide.API.Serialization;

namespace SafeRide.UnitTests;

public sealed class UtcDateTimeJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new UtcDateTimeJsonConverter() }
    };

    [Fact]
    public void Serialize_UnspecifiedDatabaseValue_WritesUtcDesignator()
    {
        var value = new DateTime(2026, 8, 12, 10, 15, 0, DateTimeKind.Unspecified);

        var json = JsonSerializer.Serialize(value, Options);

        Assert.Equal("\"2026-08-12T10:15:00.0000000Z\"", json);
    }

    [Fact]
    public void Deserialize_OffsetlessApiValue_TreatsItAsUtc()
    {
        var value = JsonSerializer.Deserialize<DateTime>(
            "\"2026-08-12T10:15:00\"",
            Options);

        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(new DateTime(2026, 8, 12, 10, 15, 0, DateTimeKind.Utc), value);
    }
}
