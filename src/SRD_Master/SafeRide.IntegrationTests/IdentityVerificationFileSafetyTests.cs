using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SafeRide.API.Controllers;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.IdentityVerification.DTOs;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.IntegrationTests;

public sealed class IdentityVerificationFileSafetyTests
{
    [Theory]
    [InlineData(FileSafetyScanStatus.ThreatDetected, "identity_verification.malware_detected", 400)]
    [InlineData(FileSafetyScanStatus.ScannerUnavailable, "identity_verification.scanner_unavailable", 503)]
    public async Task UploadDocument_UnsafeScannerOutcome_DoesNotStoreOrPersist(
        FileSafetyScanStatus status,
        string expectedCode,
        int expectedStatus)
    {
        await using var dbContext = CreateDbContext();
        var storage = new TrackingIdentityDocumentStorage();
        var scanner = new TestFileSafetyScanner(status);
        var controller = CreateController(
            dbContext, storage, TestEvidenceValidation.Create(scanner), Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            controller.UploadDocument(
                KycDocumentType.CRIMINAL_RECORD,
                Request(),
                CancellationToken.None));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.Equal(0, storage.SaveCalls);
        Assert.Empty(dbContext.DriverKycs);
    }

    [Fact]
    public async Task UploadDocument_WhenScannerDrainsStream_StoresFullContent()
    {
        await using var dbContext = CreateDbContext();
        var storage = new TrackingIdentityDocumentStorage();
        var scanner = new TestFileSafetyScanner(FileSafetyScanStatus.Clean, drainStream: true);
        var controller = CreateController(
            dbContext, storage, TestEvidenceValidation.Create(scanner), Guid.NewGuid());

        var result = await controller.UploadDocument(
            KycDocumentType.CRIMINAL_RECORD,
            Request(),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF, 1 }, storage.StoredBytes);
        Assert.Single(dbContext.DriverKycs);
    }

    [Fact]
    public async Task UploadDocument_WhenPersistenceFails_DeletesOrphanedStorage()
    {
        await using var dbContext = CreateDbContext(new ThrowOnSaveInterceptor());
        var storage = new TrackingIdentityDocumentStorage();
        var controller = CreateController(
            dbContext, storage, TestEvidenceValidation.Create(), Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.UploadDocument(
                KycDocumentType.CRIMINAL_RECORD,
                Request(),
                CancellationToken.None));

        Assert.Equal(1, storage.SaveCalls);
        Assert.Equal(["identity/file-1"], storage.DeletedStorageKeys);
    }

    [Fact]
    public async Task UploadDocument_Unauthenticated_IsRejectedBeforeScannerAndStorage()
    {
        await using var dbContext = CreateDbContext();
        var storage = new TrackingIdentityDocumentStorage();
        var scanner = new TestFileSafetyScanner(FileSafetyScanStatus.Clean);
        var controller = CreateController(
            dbContext, storage, TestEvidenceValidation.Create(scanner), userId: null);

        var result = await controller.UploadDocument(
            KycDocumentType.CRIMINAL_RECORD,
            Request(),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Equal(0, scanner.Calls);
        Assert.Equal(0, storage.SaveCalls);
    }

    private static IdentityVerificationController CreateController(
        ApplicationDbContext dbContext,
        IIdentityDocumentStorage storage,
        IEvidenceFileValidator validator,
        Guid? userId)
    {
        var identity = userId.HasValue
            ? new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())], "Test")
            : new ClaimsIdentity();
        return new IdentityVerificationController(
            dbContext,
            storage,
            validator,
            NullLogger<IdentityVerificationController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    private static UploadIdentityDocumentRequest Request()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 1 };
        return new UploadIdentityDocumentRequest
        {
            File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "criminal-record.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            }
        };
    }

    private static ApplicationDbContext CreateDbContext(IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"identity-file-safety-{Guid.NewGuid():N}");
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        return new ApplicationDbContext(builder.Options, new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
    }

    private sealed class TrackingIdentityDocumentStorage : IIdentityDocumentStorage
    {
        public int SaveCalls { get; private set; }
        public byte[]? StoredBytes { get; private set; }
        public List<string> DeletedStorageKeys { get; } = [];

        public async Task<StoredIdentityDocumentFile> SaveAsync(
            Guid driverId,
            KycDocumentType documentType,
            string slot,
            string originalFileName,
            string contentType,
            Stream content,
            CancellationToken cancellationToken)
        {
            SaveCalls++;
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            StoredBytes = copy.ToArray();
            return new StoredIdentityDocumentFile(
                "https://storage.test/identity/file-1",
                originalFileName,
                contentType,
                StoredBytes.Length,
                "identity/file-1");
        }

        public Task DeleteAsync(
            string storageKey,
            string contentType,
            CancellationToken cancellationToken)
        {
            DeletedStorageKeys.Add(storageKey);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowOnSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult<int>>(
                new InvalidOperationException("Simulated persistence failure."));
    }
}
