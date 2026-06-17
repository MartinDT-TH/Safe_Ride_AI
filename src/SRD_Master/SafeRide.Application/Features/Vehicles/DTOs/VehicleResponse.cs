using System.Text.Json.Serialization;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Vehicles.DTOs;

public sealed class VehicleResponse
{
    public long Id { get; set; }
    public string BrandModel { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string? Color { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VehicleType VehicleType { get; set; }
    public int? EngineCapacityCc { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RequiredLicenseClass RequiredLicenseClass { get; set; }
}
