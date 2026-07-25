using System.Text.Json;
using SafeRide.API.Serialization;
using SafeRide.Domain.Enums;

namespace SafeRide.UnitTests;

public sealed class MapTravelModeJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    [Theory]
    [InlineData("Car", MapTravelMode.Car)]
    [InlineData("car", MapTravelMode.Car)]
    [InlineData("Drive", MapTravelMode.Car)]
    [InlineData("Driving", MapTravelMode.Car)]
    [InlineData("DRIVE", MapTravelMode.Car)]
    [InlineData("Motorcycle", MapTravelMode.Motorcycle)]
    [InlineData("Motorbike", MapTravelMode.Motorcycle)]
    [InlineData("Moto", MapTravelMode.Motorcycle)]
    [InlineData("TwoWheeler", MapTravelMode.Motorcycle)]
    [InlineData("TWO_WHEELER", MapTravelMode.Motorcycle)]
    [InlineData("Bike", MapTravelMode.Bike)]
    [InlineData("Bicycle", MapTravelMode.Bike)]
    [InlineData("Cycling", MapTravelMode.Bike)]
    [InlineData("BICYCLE", MapTravelMode.Bike)]
    [InlineData("Foot", MapTravelMode.Foot)]
    [InlineData("Walk", MapTravelMode.Foot)]
    [InlineData("Walking", MapTravelMode.Foot)]
    [InlineData("Pedestrian", MapTravelMode.Foot)]
    [InlineData("WALK", MapTravelMode.Foot)]
    public void Deserialize_AcceptsSupportedAliases(
        string value,
        MapTravelMode expected)
    {
        var actual = JsonSerializer.Deserialize<MapTravelMode>(
            JsonSerializer.Serialize(value),
            Options);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Deserialize_NullableNull_ReturnsNull()
    {
        var actual = JsonSerializer.Deserialize<MapTravelMode?>("null", Options);

        Assert.Null(actual);
    }

    [Theory]
    [InlineData("\"Plane\"")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    public void Deserialize_InvalidValue_ThrowsJsonException(string json)
    {
        var exception = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<MapTravelMode>(json, Options));

        Assert.Contains("travelMode", exception.Message);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new MapTravelModeJsonConverter());
        return options;
    }
}
