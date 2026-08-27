using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Auth.Services;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Application.Features.Vehicles.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.IntegrationTests;

[Trait(SqlServerTestDatabase.ProviderTraitName, SqlServerTestDatabase.SqliteProvider)]
public sealed class RiskProtectionApiAuthorizationTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = CreateApiJsonOptions();

    [Fact]
    public async Task AccidentHttpAuthorizationMatrix_AllowsFourSupportedRolesAndRejectsOthers()
    {
        using var rootFactory = new AuthApiFactory();
        using var factory = CreateAccidentFactory(rootFactory, new AcceptedFileSafetyScanner());

        using (var anonymous = factory.CreateClient())
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await anonymous.GetAsync("/api/accidents/42")).StatusCode);
        }

        foreach (var role in new[] { "Customer", "Driver", "Staff", "Admin" })
        {
            using var client = await CreateClientAsync(factory, Guid.NewGuid(), role);
            Assert.Equal(
                HttpStatusCode.Created,
                (await client.PostAsJsonAsync(
                    "/api/trips/42/accidents",
                    new CreateAccidentRequest(
                        AccidentCategory.MULTIPLE,
                        DateTime.UtcNow,
                        null,
                        null,
                        "HTTP authorization matrix",
                        null))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/accidents/42")).StatusCode);
            Assert.Equal(
                HttpStatusCode.Created,
                (await client.PostAsync(
                    "/api/accidents/42/evidence",
                    ValidJpegMultipart())).StatusCode);
        }

        using var unsupported = await CreateClientAsync(factory, Guid.NewGuid(), "Guest");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await unsupported.GetAsync("/api/accidents/42")).StatusCode);
    }

    [Fact]
    public async Task AccidentEvidenceHttp_RejectsSignatureMimeSizeAndMalware_WithProblemDetails()
    {
        using var rootFactory = new AuthApiFactory();
        using (var factory = CreateAccidentFactory(rootFactory, new AcceptedFileSafetyScanner()))
        using (var client = await CreateClientAsync(factory, Guid.NewGuid(), "Driver"))
        {
            using var invalidSignature = Multipart(
                [0xFF, 0xD8, 0xFF], "evidence.png", "image/png");
            var response = await client.PostAsync("/api/accidents/42/evidence", invalidSignature);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertProblemDetailsAsync(response, "accident.evidence_invalid");

            using var invalidMime = Multipart(
                [0xFF, 0xD8, 0xFF], "evidence.bin", "application/octet-stream");
            Assert.Equal(
                HttpStatusCode.BadRequest,
                (await client.PostAsync("/api/accidents/42/evidence", invalidMime)).StatusCode);

            using var oversized = Multipart(
                new byte[10_000_001], "large.jpg", "image/jpeg");
            Assert.Equal(
                HttpStatusCode.BadRequest,
                (await client.PostAsync("/api/accidents/42/evidence", oversized)).StatusCode);
        }

        using var malwareRootFactory = new AuthApiFactory();
        using (var malwareFactory = CreateAccidentFactory(malwareRootFactory, new MalwareFileSafetyScanner()))
        using (var malwareClient = await CreateClientAsync(malwareFactory, Guid.NewGuid(), "Driver"))
        using (var malware = ValidJpegMultipart())
        {
            var response = await malwareClient.PostAsync("/api/accidents/42/evidence", malware);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertProblemDetailsAsync(response, "accident.evidence_malware_rejected");
        }

        using var unavailableRootFactory = new AuthApiFactory();
        using (var unavailableFactory = CreateAccidentFactory(
                   unavailableRootFactory,
                   new UnavailableFileSafetyScanner()))
        using (var unavailableClient = await CreateClientAsync(
                   unavailableFactory, Guid.NewGuid(), "Driver"))
        using (var unavailable = ValidJpegMultipart())
        {
            var response = await unavailableClient.PostAsync(
                "/api/accidents/42/evidence", unavailable);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            await AssertProblemDetailsAsync(response, "accident.evidence_scanner_unavailable");
        }


        using var orphanRootFactory = new AuthApiFactory();
        var orphanStorage = new StubAccidentEvidenceStorage();
        using (var orphanFactory = CreateAccidentFactory(
                   orphanRootFactory,
                   new AcceptedFileSafetyScanner(),
                   new StubAccidentManagementService(rejectEvidence: true),
                   orphanStorage))
        using (var orphanClient = await CreateClientAsync(orphanFactory, Guid.NewGuid(), "Driver"))
        using (var orphan = ValidJpegMultipart())
        {
            Assert.Equal(
                HttpStatusCode.Conflict,
                (await orphanClient.PostAsync("/api/accidents/42/evidence", orphan)).StatusCode);
            Assert.Single(orphanStorage.DeletedPublicIds);
        }
    }

    [Fact]
    public async Task AccidentEvidenceHttp_StorageMayCloseStreamWithoutBreakingResponse()
    {
        using var rootFactory = new AuthApiFactory();
        var storage = new StubAccidentEvidenceStorage(disposeContentOnSave: true);
        using var factory = CreateAccidentFactory(
            rootFactory,
            new AcceptedFileSafetyScanner(),
            evidenceStorage: storage);
        using var client = await CreateClientAsync(factory, Guid.NewGuid(), "Driver");
        using var evidence = ValidJpegMultipart();

        var response = await client.PostAsync("/api/accidents/42/evidence", evidence);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(4, storage.LastFileSizeBytes);
    }

    [Fact]
    public async Task ConcurrentStaffClaimUpdate_ReturnsStable409ProblemDetails()
    {
        using var rootFactory = new AuthApiFactory();
        using var factory = CreateAccidentFactory(
            rootFactory,
            new AcceptedFileSafetyScanner(),
            new StubAccidentManagementService(rejectClaimConcurrency: true));
        using var client = await CreateClientAsync(factory, Guid.NewGuid(), "Staff");

        var response = await client.PostAsJsonAsync(
            "/api/staff/claims/42/calculate",
            new CalculateClaimRequest(100_000m, 100_000m, 0m, 0m, false, "stale-row-version"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await AssertProblemDetailsAsync(response, "risk_protection.concurrency_conflict");
    }

    [Theory]
    [InlineData("/api/staff/claims/42/recoveries", true)]
    [InlineData("/api/staff/claims/42/write-offs", false)]
    public async Task ClaimEvidenceHttp_MalwareAndUnavailable_DoNotUpload(
        string endpoint,
        bool recovery)
    {
        foreach (var (scanner, expectedStatus, expectedCode) in new (IFileSafetyScanner, HttpStatusCode, string)[]
        {
            (new MalwareFileSafetyScanner(), HttpStatusCode.BadRequest, "risk_protection.evidence_malware_detected"),
            (new UnavailableFileSafetyScanner(), HttpStatusCode.ServiceUnavailable, "risk_protection.evidence_scanner_unavailable")
        })
        {
            using var rootFactory = new AuthApiFactory();
            var storage = new StubAccidentEvidenceStorage();
            using var factory = CreateAccidentFactory(
                rootFactory, scanner, evidenceStorage: storage);
            using var client = await CreateClientAsync(factory, Guid.NewGuid(), "Staff");
            using var request = ClaimEvidenceRequest(endpoint, recovery);

            var response = await client.SendAsync(request);

            Assert.Equal(expectedStatus, response.StatusCode);
            await AssertProblemDetailsAsync(response, expectedCode);
            Assert.Equal(0, storage.SaveCalls);
        }
    }

    [Theory]
    [InlineData("/api/staff/claims/42/recoveries", true)]
    [InlineData("/api/staff/claims/42/write-offs", false)]
    public async Task ClaimEvidenceHttp_WhenPersistenceFails_DeletesUpload(
        string endpoint,
        bool recovery)
    {
        using var rootFactory = new AuthApiFactory();
        var storage = new StubAccidentEvidenceStorage();
        using var factory = CreateAccidentFactory(
            rootFactory, new AcceptedFileSafetyScanner(), evidenceStorage: storage);
        using var client = await CreateClientAsync(factory, Guid.NewGuid(), "Staff");
        using var request = ClaimEvidenceRequest(endpoint, recovery);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            (await client.SendAsync(request)).StatusCode);
        Assert.Equal(1, storage.SaveCalls);
        Assert.Single(storage.DeletedPublicIds);
    }

    [Fact]
    public async Task InsuranceApi_EnforcesCustomerOwnershipAndStaffOnlyVerificationWithAudit()
    {
        using var factory = new AuthApiFactory();
        var ownerId = Guid.NewGuid();
        var ownerClient = await CreateClientAsync(factory, ownerId, "Customer");
        var vehicleResponse = await ownerClient.PostAsJsonAsync(
            "/api/vehicles",
            new SaveVehicleRequest
            {
                BrandModel = "Honda Vision",
                PlateNumber = "29A123456",
                Color = "Đen",
                VehicleType = VehicleType.Motorbike,
                EngineCapacityCc = 110
            });
        Assert.Equal(HttpStatusCode.Created, vehicleResponse.StatusCode);
        var vehicle = (await vehicleResponse.Content.ReadFromJsonAsync<VehicleResponse>())!;
        var policyRequest = new VehicleInsurancePolicyRequest(
            VehicleInsuranceType.PHYSICAL_DAMAGE,
            "Safe Insurer",
            $"POL-{Guid.NewGuid():N}",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddYears(1),
            20_000_000m,
            500_000m,
            "https://storage.test/policy.pdf");

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/vehicles/{vehicle.Id}/insurance-policies",
            policyRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var policy = (await createResponse.Content
            .ReadFromJsonAsync<VehicleInsurancePolicyResponse>(ApiJsonOptions))!;

        using var outsiderClient = await CreateClientAsync(factory, Guid.NewGuid(), "Customer");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await outsiderClient.GetAsync(
                $"/api/vehicles/{vehicle.Id}/insurance-policies")).StatusCode);

        foreach (var role in new[] { "Driver", "Staff", "Admin" })
        {
            using var client = await CreateClientAsync(factory, Guid.NewGuid(), role);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.GetAsync(
                    $"/api/vehicles/{vehicle.Id}/insurance-policies")).StatusCode);
        }

        foreach (var role in new[] { "Customer", "Driver", "Admin" })
        {
            using var client = role == "Customer"
                ? await CreateClientAsync(factory, ownerId, role)
                : await CreateClientAsync(factory, Guid.NewGuid(), role);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.PutAsJsonAsync(
                    $"/api/staff/vehicle-insurance-policies/{policy.Id}/verification",
                    new { status = InsuranceVerificationStatus.VERIFIED })).StatusCode);
        }

        var staffId = Guid.NewGuid();
        using var staffClient = await CreateClientAsync(factory, staffId, "Staff");
        var reviewResponse = await staffClient.PutAsJsonAsync(
            $"/api/staff/vehicle-insurance-policies/{policy.Id}/verification",
            new { status = InsuranceVerificationStatus.VERIFIED });
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        var reviewed = (await reviewResponse.Content
            .ReadFromJsonAsync<VehicleInsurancePolicyResponse>(ApiJsonOptions))!;
        Assert.Equal(InsuranceVerificationStatus.VERIFIED, reviewed.VerificationStatus);
        Assert.Equal(staffId, reviewed.ReviewedByUserId);
        Assert.NotNull(reviewed.ReviewedAtUtc);
    }

    [Fact]
    public async Task PreTripHttpAuthorizationMatrix_AllowsDriverMutationAndAuthenticatedReadOnlyRoles()
    {
        using var rootFactory = new AuthApiFactory();
        using var factory = rootFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPreTripVehicleCheckService>();
                services.AddSingleton<IPreTripVehicleCheckService, StubPreTripVehicleCheckService>();
            }));
        const long tripId = 42;
        var request = new PreTripVehicleCheckRequest(
            true, true, true, true, true, true, true, null, "All clear", null);

        foreach (var role in new[] { "Customer", "Staff", "Admin" })
        {
            using var client = await CreateClientAsync(factory, Guid.NewGuid(), role);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.PostAsJsonAsync(
                    $"/api/trips/{tripId}/vehicle-safety-checks",
                    request)).StatusCode);
        }

        using (var driverClient = await CreateClientAsync(factory, Guid.NewGuid(), "Driver"))
        {
            Assert.Equal(
                HttpStatusCode.Created,
                (await driverClient.PostAsJsonAsync(
                    $"/api/trips/{tripId}/vehicle-safety-checks",
                    request)).StatusCode);
        }

        foreach (var role in new[] { "Driver", "Customer", "Staff", "Admin" })
        {
            using var client = await CreateClientAsync(factory, Guid.NewGuid(), role);
            Assert.Equal(
                HttpStatusCode.OK,
                (await client.GetAsync(
                    $"/api/trips/{tripId}/vehicle-safety-checks")).StatusCode);
        }
    }

    [Fact]
    public async Task SafetyReportHttp_IsDriverOnlyAndPreservesExplicitReportType()
    {
        using var rootFactory = new AuthApiFactory();
        using var factory = rootFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISafetyReportService>();
                services.AddSingleton<ISafetyReportService, StubSafetyReportService>();
            }));
        var request = new SafetyReportRequest(
            SafetyReportType.UNSAFE_CUSTOMER,
            "THREATENING_BEHAVIOR",
            "Customer threatened the driver",
            null,
            null,
            false);

        foreach (var role in new[] { "Customer", "Staff", "Admin" })
        {
            using var client = await CreateClientAsync(factory, Guid.NewGuid(), role);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.PostAsJsonAsync("/api/trips/42/safety-reports", request)).StatusCode);
        }

        using var driver = await CreateClientAsync(factory, Guid.NewGuid(), "Driver");
        var response = await driver.PostAsJsonAsync("/api/trips/42/safety-reports", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SafetyReportResponse>(ApiJsonOptions);
        Assert.Equal(SafetyReportType.UNSAFE_CUSTOMER, body!.ReportType);
        Assert.False(body.EscalationRequested);
        Assert.Null(body.SosAlertId);
    }

    [Fact]
    public async Task SafetyTerminationAndManualRefund_EnforceDriverStaffAndStaffOnlyRoles()
    {
        using var rootFactory = new AuthApiFactory();
        using var factory = rootFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITripStatusService>();
                services.RemoveAll<ISafetyPaymentReconciliationService>();
                services.AddSingleton<ITripStatusService, StubTripStatusService>();
                services.AddSingleton<ISafetyPaymentReconciliationService, StubSafetyReconciliationService>();
            }));

        foreach (var role in new[] { "Customer", "Admin" })
        {
            using var client = await CreateClientAsync(factory, Guid.NewGuid(), role);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.PostAsJsonAsync(
                    "/api/trips/42/safety-termination",
                    new { reason = "Safety concern" })).StatusCode);
        }
        foreach (var role in new[] { "Driver", "Staff" })
        {
            using var client = await CreateClientAsync(factory, Guid.NewGuid(), role);
            Assert.Equal(
                HttpStatusCode.NoContent,
                (await client.PostAsJsonAsync(
                    "/api/trips/42/safety-termination",
                    new { reason = "Safety concern" })).StatusCode);
        }

        var refundRequest = new ManualRefundConfirmationRequest(
            "REF-42", "https://evidence.test/refund.pdf", "refund-42", "");
        foreach (var role in new[] { "Customer", "Driver", "Admin" })
        {
            using var client = await CreateClientAsync(factory, Guid.NewGuid(), role);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.PostAsJsonAsync(
                    "/api/staff/payments/refunds/7/confirm", refundRequest)).StatusCode);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.GetAsync("/api/staff/payments/refunds")).StatusCode);
        }
        using var staffClient = await CreateClientAsync(factory, Guid.NewGuid(), "Staff");
        Assert.Equal(
            HttpStatusCode.OK,
            (await staffClient.PostAsJsonAsync(
                "/api/staff/payments/refunds/7/confirm", refundRequest)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await staffClient.GetAsync("/api/staff/payments/refunds?status=REFUND_PENDING")).StatusCode);
    }

    private static async Task<HttpClient> CreateClientAsync(
        WebApplicationFactory<Program> factory,
        Guid userId,
        string role)
    {
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? new AspNetUser
        {
            Id = userId,
            UserName = $"{role}-{userId:N}@test.local",
            Email = $"{role}-{userId:N}@test.local",
            FullName = role,
            PhoneNumber = $"+84{Math.Abs(userId.GetHashCode()):D9}",
            PhoneNumberConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        if (await userManager.FindByIdAsync(userId.ToString()) is null)
        {
            Assert.True((await userManager.CreateAsync(user)).Succeeded);
        }
        var token = await tokenService.GenerateAccessTokenAsync(
            user,
            [role]);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }

    private static JsonSerializerOptions CreateApiJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static WebApplicationFactory<Program> CreateAccidentFactory(
        AuthApiFactory rootFactory,
        IFileSafetyScanner scanner,
        IAccidentManagementService? accidentService = null,
        IAccidentEvidenceStorage? evidenceStorage = null)
    {
        accidentService ??= new StubAccidentManagementService();
        evidenceStorage ??= new StubAccidentEvidenceStorage();
        return rootFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAccidentManagementService>();
                services.RemoveAll<IAccidentEvidenceStorage>();
                services.RemoveAll<IFileSafetyScanner>();
                services.AddSingleton(accidentService);
                services.AddSingleton(evidenceStorage);
                services.AddSingleton(scanner);
            }));
    }

    private static MultipartFormDataContent ValidJpegMultipart() =>
        Multipart([0xFF, 0xD8, 0xFF, 0x00], "evidence.jpg", "image/jpeg");

    private static MultipartFormDataContent Multipart(
        byte[] bytes,
        string fileName,
        string contentType)
    {
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(file, "file", fileName);
        multipart.Add(new StringContent("PHOTO", Encoding.UTF8), "evidenceType");
        return multipart;
    }

    private static HttpRequestMessage ClaimEvidenceRequest(string endpoint, bool recovery)
    {
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0x00]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        multipart.Add(file, "evidence", "evidence.jpg");
        multipart.Add(new StringContent("1000"), "amount");
        multipart.Add(new StringContent("row-version"), "rowVersion");
        if (recovery)
        {
            multipart.Add(new StringContent("DRIVER"), "sourceType");
            multipart.Add(new StringContent(Guid.NewGuid().ToString()), "payerReference");
            multipart.Add(new StringContent("payment-42"), "paymentReference");
        }
        else
        {
            multipart.Add(new StringContent("Write off test"), "reason");
        }
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = multipart };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return request;
    }

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        string expectedCode)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        var detail = problem.GetProperty("detail").GetString();
        Assert.False(string.IsNullOrWhiteSpace(detail));
        Assert.Matches("[À-ỹ]", detail!);
    }

    private sealed class AcceptedFileSafetyScanner : IFileSafetyScanner
    {
        public Task<FileSafetyScanResult> ScanAsync(
            string fileName, string contentType, Stream content, CancellationToken cancellationToken) =>
            Task.FromResult(new FileSafetyScanResult(FileSafetyScanStatus.Clean));
    }

    private sealed class MalwareFileSafetyScanner : IFileSafetyScanner
    {
        public Task<FileSafetyScanResult> ScanAsync(
            string fileName, string contentType, Stream content, CancellationToken cancellationToken) =>
            Task.FromResult(new FileSafetyScanResult(
                FileSafetyScanStatus.ThreatDetected,
                "test-signature"));
    }

    private sealed class UnavailableFileSafetyScanner : IFileSafetyScanner
    {
        public Task<FileSafetyScanResult> ScanAsync(
            string fileName, string contentType, Stream content, CancellationToken cancellationToken) =>
            Task.FromResult(new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable));
    }

    private sealed class StubAccidentEvidenceStorage : IAccidentEvidenceStorage
    {
        private readonly bool _disposeContentOnSave;

        public StubAccidentEvidenceStorage(bool disposeContentOnSave = false) =>
            _disposeContentOnSave = disposeContentOnSave;

        public List<string> DeletedPublicIds { get; } = [];
        public int SaveCalls { get; private set; }
        public long? LastFileSizeBytes { get; private set; }

        public async Task<StoredAccidentEvidenceFile> SaveAsync(
            long accidentId,
            string originalFileName,
            string contentType,
            long fileSizeBytes,
            Stream content,
            CancellationToken cancellationToken)
        {
            SaveCalls++;
            LastFileSizeBytes = fileSizeBytes;
            if (_disposeContentOnSave) await content.DisposeAsync();
            return new StoredAccidentEvidenceFile(
                "https://storage.test/evidence",
                $"test/{accidentId}/{Guid.NewGuid():N}",
                fileSizeBytes);
        }

        public Task DeleteAsync(string publicId, string contentType, CancellationToken cancellationToken)
        {
            DeletedPublicIds.Add(publicId);
            return Task.CompletedTask;
        }
    }

    private sealed class StubAccidentManagementService : IAccidentManagementService
    {
        private readonly bool _rejectEvidence;
        private readonly bool _rejectClaimConcurrency;

        public StubAccidentManagementService(
            bool rejectEvidence = false,
            bool rejectClaimConcurrency = false)
        {
            _rejectEvidence = rejectEvidence;
            _rejectClaimConcurrency = rejectClaimConcurrency;
        }

        public Task<AccidentResponse> CreateAsync(
            Guid userId, bool isManagement, long tripId, CreateAccidentRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Accident(userId, tripId));

        public Task<AccidentResponse> GetAsync(
            Guid userId, bool isManagement, long accidentId, CancellationToken cancellationToken) =>
            Task.FromResult(Accident(userId, 42));

        public Task EnsureCanUploadEvidenceAsync(
            Guid userId, bool isManagement, long accidentId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<AccidentEvidenceResponse> AddEvidenceAsync(
            Guid userId, bool isManagement, long accidentId, AddAccidentEvidenceRequest request, CancellationToken cancellationToken)
        {
            if (_rejectEvidence)
                throw new SafeRide.Application.Features.Bookings.BookingException(
                    "accident.evidence_limit_reached",
                    "Hồ sơ đã đạt giới hạn bằng chứng.",
                    409);
            return Task.FromResult(new AccidentEvidenceResponse(
                1, userId, request.EvidenceType, request.FileUrl, request.OriginalFileName,
                request.ContentType, request.FileSizeBytes, request.CapturedAtUtc,
                request.Latitude, request.Longitude, request.Description, DateTime.UtcNow));
        }

        public Task<IReadOnlyList<AccidentResponse>> GetStaffQueueAsync(
            AccidentQueueFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AccidentResponse>>([]);

        public Task<ProtectionClaimResponse> SaveAssessmentAsync(Guid staffUserId, long accidentId, LiabilityAssessmentRequest request, bool confirm, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProtectionClaimResponse> CalculateClaimAsync(Guid staffUserId, long claimId, CalculateClaimRequest request, CancellationToken cancellationToken)
        {
            if (_rejectClaimConcurrency)
                throw new SafeRide.Application.Features.Bookings.BookingException(
                    "risk_protection.concurrency_conflict",
                    "Dữ liệu đã được Staff khác cập nhật. Vui lòng tải lại trước khi tiếp tục.",
                    409);
            throw new NotSupportedException();
        }
        public Task<ProtectionClaimResponse> ReviewMockInsuranceAsync(Guid staffUserId, long claimId, InsuranceReviewRequest request, bool approve, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProtectionClaimResponse> RefreshMockInsuranceStatusAsync(Guid staffUserId, long claimId, string rowVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<InsuranceProviderAuditResponse>> GetInsuranceAuditsAsync(long claimId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProtectionClaimResponse> FundClaimAsync(Guid staffUserId, long claimId, string idempotencyKey, string rowVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProtectionClaimResponse> RecordRecoveryAsync(Guid staffUserId, long claimId, ClaimRecoveryRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProtectionClaimResponse> WriteOffAdvanceAsync(Guid staffUserId, long claimId, ClaimWriteOffRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProtectionClaimResponse> CloseClaimAsync(Guid staffUserId, long claimId, CloseClaimRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DriverLiabilityResponse>> GetDriverLiabilitiesAsync(Guid driverId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DisputeLiabilityAsync(Guid userId, long accidentId, LiabilityDisputeRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

        private static AccidentResponse Accident(Guid userId, long tripId) => new(
            42, tripId, userId, AccidentCategory.MULTIPLE, AccidentStatus.REPORTED,
            DateTime.UtcNow, null, null, "HTTP test", null, DateTime.UtcNow,
            null, null, [], null, null);
    }

    private sealed class StubPreTripVehicleCheckService : IPreTripVehicleCheckService
    {
        public Task EnsureCanCreateAsync(Guid driverId, long tripId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<PreTripVehicleCheckResponse> CreateAsync(
            Guid driverId,
            long tripId,
            PreTripVehicleCheckRequest request,
            StoredPreTripVehicleCheckEvidence? evidence,
            CancellationToken cancellationToken) =>
            Task.FromResult(Response(driverId, tripId));

        public Task<IReadOnlyList<PreTripVehicleCheckResponse>> GetAsync(
            Guid userId,
            bool isManagement,
            long tripId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PreTripVehicleCheckResponse>>([Response(userId, tripId)]);

        public Task EnsureCanStartAndActivateCoverageAsync(
            Guid driverId,
            Trip trip,
            DateTime startedAtUtc,
            CancellationToken cancellationToken) => Task.CompletedTask;

        private static PreTripVehicleCheckResponse Response(Guid driverId, long tripId) => new(
            1,
            tripId,
            driverId,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            PreTripCheckResult.PASS,
            null,
            "All clear",
            null,
            null,
            null,
            null,
            DateTime.UtcNow);
    }

    private sealed class StubSafetyReportService : ISafetyReportService
    {
        public Task<SafetyReportResponse> CreateAsync(
            Guid driverId,
            long tripId,
            SafetyReportRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SafetyReportResponse(
                1,
                tripId,
                request.ReportType,
                request.ReasonCode,
                request.EscalationRequested,
                request.EscalationRequested ? 7 : null,
                DateTime.UtcNow));
    }

    private sealed class StubTripStatusService : ITripStatusService
    {
        public Task SafetyTerminateAsync(Guid userId, bool isStaff, long tripId, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SafetyTerminateAsync(Guid userId, bool isStaff, long tripId, string reason, IReadOnlyList<StoredSafetyTerminationEvidence> evidence, CancellationToken cancellationToken) => Task.CompletedTask;
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

    private sealed class StubSafetyReconciliationService : ISafetyPaymentReconciliationService
    {
        public Task<IReadOnlyList<ManualRefundQueueItemResponse>> ListRefundsAsync(
            ManualRefundStatus? status, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ManualRefundQueueItemResponse>>([]);

        public Task<SafetyPaymentReconciliation> ReconcileAsync(Trip trip, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SafetyPaymentReconciliationResponse> ConfirmManualRefundAsync(
            Guid staffUserId, long refundId, ManualRefundConfirmationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new SafetyPaymentReconciliationResponse(
                42, 0, 100, 0, 100, 0,
                SafetyPaymentReconciliationStatus.REFUNDED,
                refundId, ManualRefundStatus.REFUNDED, ""));
    }
}
