using System.Text.Json;
using System.Text.Json.Serialization;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Serialization;

public sealed class MapTravelModeJsonConverter : JsonConverter<MapTravelMode>
{
    public const string SupportedValuesMessage =
        "travelMode phải là một trong các giá trị: Car, Drive, Driving, Motorcycle, Motorbike, Moto, TwoWheeler, Bike, Bicycle, Cycling, Foot, Walk, Walking hoặc Pedestrian.";

    public override MapTravelMode Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(SupportedValuesMessage);
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException(SupportedValuesMessage);
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "CAR" or "DRIVE" or "DRIVING" => MapTravelMode.Car,
            "MOTORCYCLE" or "MOTORBIKE" or "MOTO" or "TWOWHEELER" or "TWO_WHEELER"
                => MapTravelMode.Motorcycle,
            "BIKE" or "BICYCLE" or "CYCLING" => MapTravelMode.Bike,
            "FOOT" or "WALK" or "WALKING" or "PEDESTRIAN" => MapTravelMode.Foot,
            _ => throw new JsonException(SupportedValuesMessage)
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        MapTravelMode value,
        JsonSerializerOptions options)
    {
        var serializedValue = value switch
        {
            MapTravelMode.Car => nameof(MapTravelMode.Car),
            MapTravelMode.Motorcycle => nameof(MapTravelMode.Motorcycle),
            MapTravelMode.Bike => nameof(MapTravelMode.Bike),
            MapTravelMode.Foot => nameof(MapTravelMode.Foot),
            _ => throw new JsonException($"Giá trị {nameof(MapTravelMode)} không hợp lệ.")
        };

        writer.WriteStringValue(serializedValue);
    }
}
