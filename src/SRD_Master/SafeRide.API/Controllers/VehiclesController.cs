using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Vehicles.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize]
[Route("api/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IVehicleLicenseRequirementService _licenseRequirementService;

    public VehiclesController(
        ApplicationDbContext dbContext,
        IVehicleLicenseRequirementService licenseRequirementService)
    {
        _dbContext = dbContext;
        _licenseRequirementService = licenseRequirementService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var vehicles = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(vehicles);
    }

    [HttpPost]
    public async Task<ActionResult<VehicleResponse>> Create(
        SaveVehicleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var plateNumber = NormalizePlateNumber(request.PlateNumber);
        if (await PlateNumberExists(plateNumber, null, cancellationToken))
        {
            return Conflict(new
            {
                code = "vehicle.plate_number_exists",
                message = "Biển số xe đã được sử dụng."
            });
        }

        if (!TryResolveRequiredLicenseClass(
            request,
            out var requiredLicenseClass,
            out var validationError))
        {
            return validationError;
        }

        var vehicle = new Vehicle
        {
            OwnerUserId = userId,
            BrandModel = request.BrandModel.Trim(),
            PlateNumber = plateNumber,
            Color = NormalizeOptional(request.Color),
            VehicleType = request.VehicleType,
            RequiredLicenseClass = requiredLicenseClass,
            EngineCapacityCc = NormalizeEngineCapacity(request),
            EngineType = EngineType.ICE,
            TransmissionType = TransmissionType.Automatic,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Vehicles.Add(vehicle);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetAll), ToResponse(vehicle));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<VehicleResponse>> Update(
        long id,
        SaveVehicleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var vehicle = await FindOwnedVehicle(id, userId, cancellationToken);
        if (vehicle == null)
        {
            return NotFound();
        }

        var plateNumber = NormalizePlateNumber(request.PlateNumber);
        if (await PlateNumberExists(plateNumber, id, cancellationToken))
        {
            return Conflict(new
            {
                code = "vehicle.plate_number_exists",
                message = "Biển số xe đã được sử dụng."
            });
        }

        if (!TryResolveRequiredLicenseClass(
            request,
            out var requiredLicenseClass,
            out var validationError))
        {
            return validationError;
        }

        vehicle.BrandModel = request.BrandModel.Trim();
        vehicle.PlateNumber = plateNumber;
        vehicle.Color = NormalizeOptional(request.Color);
        vehicle.VehicleType = request.VehicleType;
        vehicle.RequiredLicenseClass = requiredLicenseClass;
        vehicle.EngineCapacityCc = NormalizeEngineCapacity(request);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(vehicle));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var vehicle = await FindOwnedVehicle(id, userId, cancellationToken);
        if (vehicle == null)
        {
            return NotFound();
        }

        vehicle.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }

    private Task<Vehicle?> FindOwnedVehicle(
        long id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Vehicles.FirstOrDefaultAsync(
            x => x.Id == id && x.OwnerUserId == userId && !x.IsDeleted,
            cancellationToken);
    }

    private Task<bool> PlateNumberExists(
        string plateNumber,
        long? ignoredId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Vehicles.AnyAsync(
            x =>
                !x.IsDeleted &&
                x.PlateNumber == plateNumber &&
                (!ignoredId.HasValue || x.Id != ignoredId.Value),
            cancellationToken);
    }

    private static string NormalizePlateNumber(string value)
    {
        return string.Join(
            ' ',
            value.Trim().ToUpperInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private bool TryResolveRequiredLicenseClass(
        SaveVehicleRequest request,
        out RequiredLicenseClass requiredLicenseClass,
        out ActionResult<VehicleResponse> errorResult)
    {
        if (_licenseRequirementService.TryResolveRequiredLicenseClass(
            request.VehicleType,
            request.EngineCapacityCc,
            out requiredLicenseClass))
        {
            errorResult = default!;
            return true;
        }

        errorResult = BadRequest(new
        {
            code = "vehicle.license_requirement_missing",
            message = request.VehicleType == VehicleType.Motorbike
                ? "Xe máy cần dung tích xi-lanh hợp lệ để xác định hạng bằng A1 hoặc A."
                : "Không xác định được hạng bằng lái cần thiết cho xe."
        });
        return false;
    }

    private static int? NormalizeEngineCapacity(SaveVehicleRequest request)
    {
        return request.VehicleType == VehicleType.Motorbike
            ? request.EngineCapacityCc
            : null;
    }

    private static VehicleResponse ToResponse(Vehicle vehicle)
    {
        return new VehicleResponse
        {
            Id = vehicle.Id,
            BrandModel = vehicle.BrandModel,
            PlateNumber = vehicle.PlateNumber,
            Color = vehicle.Color,
            VehicleType = vehicle.VehicleType,
            EngineCapacityCc = vehicle.EngineCapacityCc,
            RequiredLicenseClass = vehicle.RequiredLicenseClass
        };
    }
}
