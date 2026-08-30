using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Infrastructure.Services;

public sealed class PrivateInsuranceDocumentStorageOptions
{
    public const string SectionName = "PrivateInsuranceDocumentStorage";
    public string RootPath { get; set; } = "App_Data/private-insurance-documents";
}

/// <summary>Private server-side storage. Only opaque keys are persisted; content is never public.</summary>
public sealed class PrivateInsuranceDocumentStorage : IPrivateInsuranceDocumentStorage
{
    private readonly string _root;

    public PrivateInsuranceDocumentStorage(
        IHostEnvironment environment,
        IOptions<PrivateInsuranceDocumentStorageOptions> options)
    {
        var configured = options.Value.RootPath?.Trim();
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "private-insurance-documents")
            : Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(environment.ContentRootPath, configured));
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredPrivateInsuranceDocument> SaveAsync(
        string aggregateType, long aggregateId, string fileName, string contentType,
        Stream content, CancellationToken cancellationToken)
    {
        var safeType = aggregateType.Equals("policy", StringComparison.OrdinalIgnoreCase) ? "policies" : "claims";
        var extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        var key = $"insurance/{safeType}/{aggregateId}/{Guid.NewGuid():N}{extension}";
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, true))
        {
            await content.CopyToAsync(destination, cancellationToken);
        }
        return new StoredPrivateInsuranceDocument(key, new FileInfo(path).Length);
    }

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stream = new FileStream(Resolve(objectKey), FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, true);
        return Task.FromResult<Stream>(stream);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(objectKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey) || objectKey.Contains('\\') || objectKey.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid private insurance document key.");
        var path = Path.GetFullPath(Path.Combine(_root, objectKey.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid private insurance document key.");
        return path;
    }
}
