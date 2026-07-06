namespace SafeRide.Application.Features.Trips.DTOs;

/// <summary>
/// Carries a single evidence photo stream from the API layer to the Application service.
/// Keeps IFormFile (ASP.NET Core) out of the Application layer.
/// </summary>
public sealed record ReturnEvidenceItem(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeBytes);
