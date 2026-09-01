using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SafeRide.API.Controllers;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.IntegrationTests;

public sealed class PreTripVehicleCheckControllerTests
{
    [Fact]
    public async Task MultipartEvidence_WithValidSignature_UsesTrustedStorageMetadata()
    {
        var service = new CapturingPreTripService();
        var storage = new CapturingPreTripEvidenceStorage();
        var controller = CreateController(service, storage);
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0x01, 0x02 };
        var file = new FormFile(new MemoryStream(jpeg), 0, jpeg.Length, "evidence", "brake.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var action = await controller.CreateVehicleSafetyCheckWithEvidence(
            42,
            false,
            true,
            true,
            true,
            true,
            true,
            true,
            VehicleFaultType.BRAKE_FAILURE,
            "Phanh không đạt",
            file,
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(action.Result);
        var response = Assert.IsType<PreTripVehicleCheckResponse>(created.Value);
        Assert.Equal("https://storage.test/pre-trip/brake.jpg", response.EvidenceUrl);
        Assert.Equal("brake.jpg", response.EvidenceOriginalFileName);
        Assert.Equal("image/jpeg", response.EvidenceContentType);
        Assert.Equal(jpeg.Length, response.EvidenceFileSizeBytes);
        Assert.True(service.EnsureCanCreateCalled);
        Assert.NotNull(service.Evidence);
    }

    [Fact]
    public async Task MultipartEvidence_WithMismatchedSignature_IsRejectedBeforeUpload()
    {
        var service = new CapturingPreTripService();
        var storage = new CapturingPreTripEvidenceStorage();
        var controller = CreateController(service, storage);
        var invalid = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        var file = new FormFile(new MemoryStream(invalid), 0, invalid.Length, "evidence", "fake.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            controller.CreateVehicleSafetyCheckWithEvidence(
                42,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                null,
                null,
                file,
                CancellationToken.None));

        Assert.Equal("pretrip.evidence_invalid", exception.Code);
        Assert.False(storage.SaveCalled);
    }

    [Fact]
    public async Task MultipartEvidence_WhenPersistenceFails_DeletesOrphanedUpload()
    {
        var service = new CapturingPreTripService { ThrowOnCreate = true };
        var storage = new CapturingPreTripEvidenceStorage();
        var controller = CreateController(service, storage);
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0x01 };
        var file = new FormFile(new MemoryStream(jpeg), 0, jpeg.Length, "evidence", "brake.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.CreateVehicleSafetyCheckWithEvidence(
                42,
                false,
                true,
                true,
                true,
                true,
                true,
                true,
                VehicleFaultType.BRAKE_FAILURE,
                null,
                file,
                CancellationToken.None));

        Assert.True(storage.SaveCalled);
        Assert.True(storage.DeleteCalled);
    }

    [Theory]
    [InlineData(FileSafetyScanStatus.ThreatDetected, "pretrip.evidence_malware_detected", 400)]
    [InlineData(FileSafetyScanStatus.ScannerUnavailable, "pretrip.evidence_scanner_unavailable", 503)]
    public async Task MultipartEvidence_UnsafeScannerOutcome_DoesNotUpload(
        FileSafetyScanStatus status,
        string expectedCode,
        int expectedStatus)
    {
        var service = new CapturingPreTripService();
        var storage = new CapturingPreTripEvidenceStorage();
        var controller = CreateController(
            service,
            storage,
            TestEvidenceValidation.Create(new TestFileSafetyScanner(status)));
        var file = JpegFile();

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            controller.CreateVehicleSafetyCheckWithEvidence(
                42, true, true, true, true, true, true, true,
                null, null, file, CancellationToken.None));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.True(service.EnsureCanCreateCalled);
        Assert.False(storage.SaveCalled);
    }

    [Fact]
    public async Task MultipartEvidence_WhenScannerDrainsStream_StorageReceivesFullContent()
    {
        var service = new CapturingPreTripService();
        var storage = new CapturingPreTripEvidenceStorage();
        var scanner = new TestFileSafetyScanner(FileSafetyScanStatus.Clean, drainStream: true);
        var controller = CreateController(
            service, storage, TestEvidenceValidation.Create(scanner));
        var file = JpegFile();

        await controller.CreateVehicleSafetyCheckWithEvidence(
            42, true, true, true, true, true, true, true,
            null, null, file, CancellationToken.None);

        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF, 0x01 }, storage.StoredBytes);
    }

    [Fact]
    public async Task SafetyTerminationMultipart_UsesTrustedUploadedEvidence()
    {
        var tripStatus = new CapturingTripStatusService();
        var safetyStorage = new CapturingSafetyEvidenceStorage();
        var driverId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, driverId.ToString()), new Claim(ClaimTypes.Role, "Driver")],
            "Test");
        var controller = new TripRiskProtectionController(
            new CapturingPreTripService(), tripStatus, null!, null!,
            new CapturingPreTripEvidenceStorage(), safetyStorage,
            TestEvidenceValidation.Create(),
            NullLogger<TripRiskProtectionController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1 };
        var file = new FormFile(new MemoryStream(png), 0, png.Length, "evidence", "unsafe.png")
        {
            Headers = new HeaderDictionary(), ContentType = "image/png"
        };

        var result = await controller.SafetyTerminateWithEvidence(
            42, "Phanh không an toàn", [file, JpegFile("second.jpg")], CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(2, safetyStorage.SaveCount);
        Assert.Equal(2, tripStatus.Evidence.Count);
        Assert.Equal("https://storage.test/safety/unsafe.png", tripStatus.Evidence[0].EvidenceUrl);
        Assert.Equal(driverId, tripStatus.UserId);
    }

    [Theory]
    [InlineData(FileSafetyScanStatus.ThreatDetected, "trip.evidence_malware_detected", 400)]
    [InlineData(FileSafetyScanStatus.ScannerUnavailable, "trip.evidence_scanner_unavailable", 503)]
    public async Task SafetyTerminationMultipart_UnsafeScannerOutcome_DoesNotUploadOrTerminate(
        FileSafetyScanStatus status,
        string expectedCode,
        int expectedStatus)
    {
        var tripStatus = new CapturingTripStatusService();
        var storage = new CapturingSafetyEvidenceStorage();
        var driverId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, driverId.ToString()), new Claim(ClaimTypes.Role, "Driver")],
            "Test");
        var controller = new TripRiskProtectionController(
            new CapturingPreTripService(), tripStatus, null!, null!,
            new CapturingPreTripEvidenceStorage(), storage,
            TestEvidenceValidation.Create(new TestFileSafetyScanner(status)),
            NullLogger<TripRiskProtectionController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            controller.SafetyTerminateWithEvidence(
                42, "Nguy cơ an toàn", [JpegFile()], CancellationToken.None));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.True(tripStatus.EnsureCanSafetyTerminateCalled);
        Assert.Equal(0, storage.SaveCount);
        Assert.Empty(tripStatus.Evidence);
    }

    [Fact]
    public async Task SafetyTerminationMultipart_WhenPersistenceFails_DeletesOrphanedUpload()
    {
        var tripStatus = new CapturingTripStatusService { ThrowOnSafetyTerminate = true };
        var storage = new CapturingSafetyEvidenceStorage();
        var driverId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, driverId.ToString()), new Claim(ClaimTypes.Role, "Driver")],
            "Test");
        var controller = new TripRiskProtectionController(
            new CapturingPreTripService(), tripStatus, null!, null!,
            new CapturingPreTripEvidenceStorage(), storage,
            TestEvidenceValidation.Create(),
            NullLogger<TripRiskProtectionController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.SafetyTerminateWithEvidence(
                42, "Nguy cơ an toàn", [JpegFile(), JpegFile("second.jpg")], CancellationToken.None));

        Assert.Equal(2, storage.SaveCount);
        Assert.Equal(2, storage.DeleteCount);
    }

    private static TripRiskProtectionController CreateController(
        IPreTripVehicleCheckService service,
        IPreTripVehicleCheckEvidenceStorage storage,
        IEvidenceFileValidator? evidenceFileValidator = null)
    {
        var driverId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, driverId.ToString()),
                new Claim(ClaimTypes.Role, "Driver")
            ],
            "Test");
        return new TripRiskProtectionController(
            service,
            null!,
            null!,
            null!,
            storage,
            evidenceFileValidator ?? TestEvidenceValidation.Create(),
            NullLogger<TripRiskProtectionController>.Instance)
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

    private static FormFile JpegFile(string fileName = "evidence.jpg")
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0x01 };
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "evidence", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    private sealed class CapturingPreTripService : IPreTripVehicleCheckService
    {
        public bool EnsureCanCreateCalled { get; private set; }
        public StoredPreTripVehicleCheckEvidence? Evidence { get; private set; }
        public bool ThrowOnCreate { get; init; }

        public Task EnsureCanCreateAsync(
            Guid driverId,
            long tripId,
            CancellationToken cancellationToken)
        {
            EnsureCanCreateCalled = true;
            return Task.CompletedTask;
        }

        public Task<PreTripVehicleCheckResponse> CreateAsync(
            Guid driverId,
            long tripId,
            PreTripVehicleCheckRequest request,
            StoredPreTripVehicleCheckEvidence? evidence,
            CancellationToken cancellationToken)
        {
            if (ThrowOnCreate) throw new InvalidOperationException("Simulated persistence failure.");
            Evidence = evidence;
            return Task.FromResult(new PreTripVehicleCheckResponse(
                1,
                tripId,
                driverId,
                request.BrakeResponsePassed,
                request.FrontRearLightsPassed,
                request.TurnSignalsPassed,
                request.VisibleTiresPassed,
                request.DashboardWarningPassed,
                request.WindshieldVisibilityPassed,
                request.NoMajorVisibleIssue,
                request.BrakeResponsePassed ? PreTripCheckResult.PASS : PreTripCheckResult.FAIL,
                request.FaultType,
                request.Note,
                evidence?.FileUrl,
                evidence?.OriginalFileName,
                evidence?.ContentType,
                evidence?.FileSizeBytes,
                DateTime.UtcNow));
        }

        public Task<IReadOnlyList<PreTripVehicleCheckResponse>> GetAsync(
            Guid userId,
            bool isManagement,
            long tripId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PreTripVehicleCheckResponse>>([]);

        public Task EnsureCanStartAndActivateCoverageAsync(
            Guid driverId,
            Trip trip,
            DateTime startedAtUtc,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CapturingSafetyEvidenceStorage : ISafetyTerminationEvidenceStorage
    {
        public int SaveCount { get; private set; }
        public int DeleteCount { get; private set; }

        public Task<StoredSafetyTerminationEvidence> SaveAsync(
            long tripId, string originalFileName, string contentType, long fileSizeBytes,
            Stream content, CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(new StoredSafetyTerminationEvidence(
                $"https://storage.test/safety/{originalFileName}",
                $"safety/{tripId}/{SaveCount}", originalFileName, contentType, fileSizeBytes));
        }

        public Task DeleteAsync(string publicId, string contentType, CancellationToken cancellationToken)
        {
            DeleteCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingTripStatusService : ITripStatusService
    {
        public Guid UserId { get; private set; }
        public IReadOnlyList<StoredSafetyTerminationEvidence> Evidence { get; private set; } = [];
        public bool EnsureCanSafetyTerminateCalled { get; private set; }
        public bool ThrowOnSafetyTerminate { get; init; }
        public Task EnsureCanSafetyTerminateAsync(Guid userId, bool isStaff, long tripId, string reason, CancellationToken cancellationToken)
        {
            EnsureCanSafetyTerminateCalled = true;
            return Task.CompletedTask;
        }
        public Task SafetyTerminateAsync(Guid userId, bool isStaff, long tripId, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SafetyTerminateAsync(Guid userId, bool isStaff, long tripId, string reason, IReadOnlyList<StoredSafetyTerminationEvidence> evidence, CancellationToken cancellationToken)
        {
            if (ThrowOnSafetyTerminate)
                throw new InvalidOperationException("Simulated persistence failure.");
            UserId = userId;
            Evidence = evidence;
            return Task.CompletedTask;
        }
        public Task UpdateDriverTripStatusAsync(Guid driverId, long tripId, TripStatus tripStatus, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task EndTripAsync(
            Guid driverId,
            long tripId,
            CancellationToken cancellationToken,
            TripEndReason reason = TripEndReason.NORMAL_COMPLETION,
            bool canContinueWorking = true) => Task.CompletedTask;
        public Task ConfirmReturnByCustomerAsync(Guid customerId, long tripId, bool vehicleReturnedConfirmed, CancellationToken cancellationToken, int? ratingScore = null, string? comment = null) => Task.CompletedTask;
        public Task ConfirmReturnByDriverAsync(Guid driverId, long tripId, IReadOnlyList<SafeRide.Application.Features.Trips.DTOs.ReturnEvidenceItem> evidence, string? note, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteTripAsync(Guid userId, long tripId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AdvanceAfterSuccessfulPaymentAsync(Guid userId, long tripId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CapturingPreTripEvidenceStorage
        : IPreTripVehicleCheckEvidenceStorage
    {
        public bool SaveCalled { get; private set; }
        public bool DeleteCalled { get; private set; }
        public byte[]? StoredBytes { get; private set; }

        public async Task<StoredPreTripVehicleCheckEvidence> SaveAsync(
            long tripId,
            string originalFileName,
            string contentType,
            long fileSizeBytes,
            Stream content,
            CancellationToken cancellationToken)
        {
            SaveCalled = true;
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            StoredBytes = copy.ToArray();
            return new StoredPreTripVehicleCheckEvidence(
                "https://storage.test/pre-trip/brake.jpg",
                "saferide/pre-trip/42/1",
                originalFileName,
                contentType,
                fileSizeBytes);
        }

        public Task DeleteAsync(
            string publicId,
            string contentType,
            CancellationToken cancellationToken)
        {
            DeleteCalled = true;
            return Task.CompletedTask;
        }
    }
}
