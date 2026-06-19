using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.IdentityVerification.DTOs;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.Services;

public sealed class LocalIdentityDocumentStorage : IIdentityDocumentStorage
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".pdf"
    };

    private readonly IHostEnvironment _environment;
    private readonly string _publicBasePath;

    public LocalIdentityDocumentStorage(
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _environment = environment;
        _publicBasePath = configuration["IdentityVerification:PublicBasePath"]
            ?? "/uploads/identity-verification";
    }

    public async Task<StoredIdentityDocumentFile> SaveAsync(
        Guid driverId,
        KycDocumentType documentType,
        string slot,
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(originalFileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Định dạng file không được hỗ trợ.");
        }

        var safeSlot = NormalizeSegment(slot);
        var fileName = $"{safeSlot}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var relativeDirectory = Path.Combine(
            "uploads",
            "identity-verification",
            driverId.ToString("N"),
            documentType.ToString().ToLowerInvariant());
        var absoluteDirectory = Path.Combine(_environment.ContentRootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var absolutePath = Path.Combine(absoluteDirectory, fileName);
        await using (var fileStream = File.Create(absolutePath))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        var publicUrl = $"{_publicBasePath.TrimEnd('/')}/{driverId:N}/{documentType.ToString().ToLowerInvariant()}/{fileName}";
        return new StoredIdentityDocumentFile(publicUrl, fileName, contentType, content.Length);
    }

    private static string NormalizeSegment(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "front" => "front",
            "back" => "back",
            "file" => "file",
            _ => "document"
        };
    }
}

