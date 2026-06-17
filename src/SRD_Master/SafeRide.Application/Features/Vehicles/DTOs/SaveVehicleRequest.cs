using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Vehicles.DTOs;

public sealed class SaveVehicleRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string BrandModel { get; set; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 4)]
    [RegularExpression(
        @"^[A-Za-z0-9 .-]+$",
        ErrorMessage = "Biển số xe chỉ được chứa chữ cái, chữ số, dấu chấm, dấu cách và dấu gạch ngang.")]
    public string PlateNumber { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Color { get; set; }

    [EnumDataType(typeof(VehicleType))]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VehicleType VehicleType { get; set; }
}
