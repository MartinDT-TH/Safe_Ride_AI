using Microsoft.Extensions.Hosting;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;

namespace SafeRide.Infrastructure.Services;

public sealed class EvidenceFileValidator : IEvidenceFileValidator
{
    private const int CopyBufferSize = 81_920;
    private readonly IFileSafetyScanner _scanner;
    private readonly IHostEnvironment _environment;

    public EvidenceFileValidator(
        IFileSafetyScanner scanner,
        IHostEnvironment environment)
    {
        _scanner = scanner;
        _environment = environment;
    }

    public async Task<ValidatedEvidenceFile> ValidateAsync(
        EvidenceFileValidationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.FileSizeBytes is <= 0 || request.FileSizeBytes > request.MaxFileSizeBytes)
        {
            throw Invalid(request, $"Tệp bằng chứng phải có dung lượng từ 1 byte đến {FormatLimit(request.MaxFileSizeBytes)}.");
        }

        var fileName = Path.GetFileName(request.FileName);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
        {
            throw Invalid(request, "Tên tệp bằng chứng không hợp lệ.");
        }

        var contentType = NormalizeContentType(request.ContentType);
        var allowedContentTypes = request.AllowedContentTypes
            .Select(NormalizeContentType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!allowedContentTypes.Contains(contentType) || !HasMatchingExtension(fileName, contentType))
        {
            throw Invalid(request, "Tên tệp hoặc định dạng MIME của bằng chứng không được hỗ trợ.");
        }

        var content = new MemoryStream(
            request.FileSizeBytes <= int.MaxValue ? (int)request.FileSizeBytes : 0);
        try
        {
            await CopyWithLimitAsync(
                request.Content,
                content,
                request.MaxFileSizeBytes,
                request.ErrorCodes.Invalid,
                cancellationToken);
            if (content.Length != request.FileSizeBytes)
            {
                throw Invalid(request, "Dung lượng tệp bằng chứng không khớp metadata tải lên.");
            }

            content.Position = 0;
            if (!await HasValidSignatureAsync(content, contentType, cancellationToken))
            {
                throw Invalid(request, "Chữ ký tệp không khớp với định dạng MIME.");
            }

            FileSafetyScanResult scan;
            try
            {
                content.Position = 0;
                scan = await _scanner.ScanAsync(
                    fileName,
                    contentType,
                    content,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                throw ScannerUnavailable(request);
            }

            if (scan is null)
            {
                throw ScannerUnavailable(request);
            }

            switch (scan.Status)
            {
                case FileSafetyScanStatus.Clean:
                    break;
                case FileSafetyScanStatus.DevelopmentBypass
                    when ((_environment.IsDevelopment()
                           || _environment.IsEnvironment("Testing"))
                          && _scanner is NonProductionFileSafetyScanner):
                    break;
                case FileSafetyScanStatus.ThreatDetected:
                    throw new BookingException(
                        request.ErrorCodes.MalwareDetected,
                        "Tệp bằng chứng bị từ chối do không đáp ứng yêu cầu an toàn.",
                        400);
                default:
                    throw ScannerUnavailable(request);
            }

            content.Position = 0;
            return new ValidatedEvidenceFile(
                fileName,
                contentType,
                content.Length,
                content);
        }
        catch
        {
            await content.DisposeAsync();
            throw;
        }
    }

    private static async Task CopyWithLimitAsync(
        Stream source,
        Stream destination,
        long maxBytes,
        string invalidCode,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferSize];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) return;
            total += read;
            if (total > maxBytes)
            {
                throw new BookingException(
                    invalidCode,
                    $"Tệp bằng chứng không được vượt quá {FormatLimit(maxBytes)}.",
                    400);
            }
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static async Task<bool> HasValidSignatureAsync(
        Stream stream,
        string contentType,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];
        var read = await stream.ReadAsync(header, cancellationToken);
        return contentType switch
        {
            "image/jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" => read >= 8 && header.AsSpan(0, 8).SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => read >= 12
                && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            "application/pdf" => read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            _ => false
        };
    }

    private static string NormalizeContentType(string contentType) =>
        (contentType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "image/jpg" => "image/jpeg",
            var normalized => normalized
        };

    private static bool HasMatchingExtension(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        return contentType switch
        {
            "image/jpeg" => extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase),
            "image/png" => extension.Equals(".png", StringComparison.OrdinalIgnoreCase),
            "image/webp" => extension.Equals(".webp", StringComparison.OrdinalIgnoreCase),
            "application/pdf" => extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static BookingException Invalid(
        EvidenceFileValidationRequest request,
        string detail) => new(request.ErrorCodes.Invalid, detail, 400);

    private static BookingException ScannerUnavailable(
        EvidenceFileValidationRequest request) => new(
            request.ErrorCodes.ScannerUnavailable,
            "Hệ thống quét an toàn tệp chưa sẵn sàng. Vui lòng thử lại sau.",
            503);

    private static string FormatLimit(long maxBytes) =>
        maxBytes % (1024 * 1024) == 0
            ? $"{maxBytes / (1024 * 1024)} MB"
            : $"{maxBytes:N0} byte";
}
