using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace SafeRide.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NoIndexing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    BanReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Gender = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Promotions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PromotionCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    DiscountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaxUsageCount = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    CurrentUsageCount = table.Column<int>(type: "int", nullable: false),
                    MinimumOrderValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaximumDiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UsageLimitPerUser = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Promotio__3214EC07CE4C05F3", x => x.Id);
                    table.CheckConstraint("CK_Promotions_Date", "[EndDate] > [StartDate]");
                    table.CheckConstraint("CK_Promotions_DiscountType", "[DiscountType] IS NULL OR [DiscountType] IN ('Percentage', 'Fixed')");
                    table.CheckConstraint("CK_Promotions_DiscountValue", "[DiscountValue] > 0");
                    table.CheckConstraint("CK_Promotions_OrderValue", "[MinimumOrderValue] >= 0 AND [MaximumDiscountValue] >= 0");
                    table.CheckConstraint("CK_Promotions_Usage", "[MaxUsageCount] > 0 AND [CurrentUsageCount] >= 0 AND [CurrentUsageCount] <= [MaxUsageCount] AND [UsageLimitPerUser] > 0");
                });

            migrationBuilder.CreateTable(
                name: "ServiceTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ServiceT__3214EC07737B7BD1", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SurgePricingRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    AppliedDays = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SurgeMultiplier = table.Column<decimal>(type: "decimal(3,2)", nullable: false, defaultValue: 1.00m),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SurgePri__3214EC075AB58DD8", x => x.Id);
                    table.CheckConstraint("CK_SurgePricingRules_SurgeMultiplier", "[SurgeMultiplier] >= 1");
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingKey = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SystemSe__3214EC071B6F3002", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_Roles",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_Users",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DriverKyc",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    LicenseClass = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FrontImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BackImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    KycStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValueSql: "('Pending')"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__DriverKy__3214EC075745B3C5", x => x.Id);
                    table.CheckConstraint("CK_DriverKyc_DocumentFile", "[FrontImageUrl] IS NOT NULL OR [BackImageUrl] IS NOT NULL OR [FileUrl] IS NOT NULL");
                    table.CheckConstraint("CK_DriverKyc_DocumentType", "[DocumentType] IN ('ID_CARD', 'DRIVING_LICENSE', 'CRIMINAL_RECORD')");
                    table.CheckConstraint("CK_DriverKyc_DrivingLicense", "[DocumentType] <> 'DRIVING_LICENSE' OR ([DocumentNumber] IS NOT NULL AND [LicenseClass] IS NOT NULL)");
                    table.CheckConstraint("CK_DriverKyc_KycStatus", "[KycStatus] IN ('Pending', 'Approved', 'Rejected')");
                    table.CheckConstraint("CK_DriverKyc_LicenseClass", "[LicenseClass] IS NULL OR [LicenseClass] IN ('A1', 'A', 'B1', 'B', 'C1', 'C', 'D1', 'D2', 'D', 'Old_B1', 'Old_B2', 'Old_A1', 'Old_A2')");
                    table.ForeignKey(
                        name: "FK_DriverKyc_AspNetUsers",
                        column: x => x.DriverId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DriverProfiles",
                columns: table => new
                {
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdentityCardNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ExperienceYears = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    HomeAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValueSql: "('Offline')"),
                    LastActiveAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__DriverPr__F1B1CD046AA3E9C5", x => x.DriverId);
                    table.CheckConstraint("CK_DriverProfiles_ExperienceYears", "[ExperienceYears] IS NULL OR [ExperienceYears] >= 0");
                    table.CheckConstraint("CK_DriverProfiles_WorkStatus", "[WorkStatus] IN ('Online', 'Offline', 'Busy')");
                    table.ForeignKey(
                        name: "FK_DriverProfiles_AspNetUsers",
                        column: x => x.DriverId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NotificationType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Notifica__3214EC07BC6B781B", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    JwtId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeviceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeviceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReplacedByTokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: true),
                    IsRevoked = table.Column<int>(type: "int", nullable: false, computedColumnSql: "(case when [RevokedAt] IS NOT NULL then (1) else (0) end)", stored: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__RefreshT__3214EC071C13A19A", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AspNetUsers",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlateNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    BrandModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiredLicenseClass = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    VehicleType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EngineType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransmissionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Vehicles__3214EC07353CD60C", x => x.Id);
                    table.CheckConstraint("CK_Vehicles_EngineType", "[EngineType] IN ('ICE', 'EV')");
                    table.CheckConstraint("CK_Vehicles_RequiredLicenseClass", "[RequiredLicenseClass] IN ('A1', 'A', 'B', 'C1', 'C', 'D1', 'D2', 'D')");
                    table.CheckConstraint("CK_Vehicles_TransmissionType", "[TransmissionType] IN ('Manual', 'Automatic', 'None')");
                    table.CheckConstraint("CK_Vehicles_VehicleType", "[VehicleType] IN ('Motorbike', 'Car')");
                    table.ForeignKey(
                        name: "FK_Vehicles_AspNetUsers",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PricingRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleClass = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ServiceTypeId = table.Column<long>(type: "bigint", nullable: false),
                    BaseFare = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinFare = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PricePerKm = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PricePerHour = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__PricingR__3214EC07CFB6D6FF", x => x.Id);
                    table.CheckConstraint("CK_PricingRules_BaseFare", "[BaseFare] >= 0");
                    table.CheckConstraint("CK_PricingRules_MinFare", "[MinFare] >= 0");
                    table.CheckConstraint("CK_PricingRules_PricePerHour", "[PricePerHour] IS NULL OR [PricePerHour] >= 0");
                    table.CheckConstraint("CK_PricingRules_PricePerKm", "[PricePerKm] IS NULL OR [PricePerKm] >= 0");
                    table.CheckConstraint("CK_PricingRules_VehicleClass", "[VehicleClass] IN ('A1', 'A', 'B', 'C1', 'C', 'D1', 'D2', 'D')");
                    table.ForeignKey(
                        name: "FK_PricingRules_ServiceType",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DriverWallets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__DriverWa__3214EC0723702C3C", x => x.Id);
                    table.CheckConstraint("CK_DriverWallets_CurrentBalance", "[CurrentBalance] >= 0");
                    table.ForeignKey(
                        name: "FK_Wallet_Driver",
                        column: x => x.DriverId,
                        principalTable: "DriverProfiles",
                        principalColumn: "DriverId");
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceTypeId = table.Column<long>(type: "bigint", nullable: false),
                    BookingType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValueSql: "('Now')"),
                    BookingStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValueSql: "('SEARCHING_DRIVER')"),
                    PricingRuleId = table.Column<long>(type: "bigint", nullable: true),
                    SurgePricingRuleId = table.Column<long>(type: "bigint", nullable: true),
                    BookingSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Manual"),
                    PickupAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PickupLocation = table.Column<Point>(type: "geography", nullable: false),
                    DestinationAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DestinationLocation = table.Column<Point>(type: "geography", nullable: true),
                    EstimatedDistanceKm = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    EstimatedDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    EstimatedFare = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SpecialRequest = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Bookings__3214EC073A8063CD", x => x.Id);
                    table.CheckConstraint("CK_Bookings_BookingSource", "[BookingSource] IN ('Manual', 'VoiceCommand', 'Scheduled')");
                    table.CheckConstraint("CK_Bookings_BookingStatus", "[BookingStatus] IN ('SEARCHING_DRIVER', 'DRIVER_ASSIGNED', 'CUSTOMER_CANCELLED', 'DRIVER_CANCELLED', 'EXPIRED', 'CONVERTED_TO_TRIP')");
                    table.CheckConstraint("CK_Bookings_BookingType", "[BookingType] IN ('Now', 'Scheduled')");
                    table.CheckConstraint("CK_Bookings_DestinationLocation", "[DestinationLocation] IS NULL OR [DestinationLocation].STSrid = 4326");
                    table.CheckConstraint("CK_Bookings_EstimatedDistanceKm", "[EstimatedDistanceKm] IS NULL OR [EstimatedDistanceKm] >= 0");
                    table.CheckConstraint("CK_Bookings_EstimatedDurationMinutes", "[EstimatedDurationMinutes] IS NULL OR [EstimatedDurationMinutes] >= 0");
                    table.CheckConstraint("CK_Bookings_EstimatedFare", "[EstimatedFare] IS NULL OR [EstimatedFare] >= 0");
                    table.CheckConstraint("CK_Bookings_PickupLocation", "[PickupLocation].STSrid = 4326");
                    table.CheckConstraint("CK_Bookings_ScheduledAt", "([BookingType] = 'Now' AND [ScheduledAt] IS NULL) OR ([BookingType] = 'Scheduled' AND [ScheduledAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Booking_ServiceType",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Booking_Vehicle",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bookings_AspNetUsers",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bookings_CancelledBy",
                        column: x => x.CancelledBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bookings_PricingRule",
                        column: x => x.PricingRuleId,
                        principalTable: "PricingRules",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bookings_SurgeRule",
                        column: x => x.SurgePricingRuleId,
                        principalTable: "SurgePricingRules",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WithdrawalRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BankAccountNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    BankAccountName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValueSql: "('Pending')"),
                    RejectionReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Withdraw__3214EC07E0586027", x => x.Id);
                    table.CheckConstraint("CK_WithdrawalRequests_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_WithdrawalRequests_Status", "[Status] IN ('Pending', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_Withdrawal_Wallet",
                        column: x => x.WalletId,
                        principalTable: "DriverWallets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BookingPromotions",
                columns: table => new
                {
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    PromotionId = table.Column<long>(type: "bigint", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__BookingP__96B958114224E6FD", x => new { x.BookingId, x.PromotionId });
                    table.CheckConstraint("CK_BookingPromotions_DiscountAmount", "[DiscountAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_BookingPromotion_Booking",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingPromotion_Promotion",
                        column: x => x.PromotionId,
                        principalTable: "Promotions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValueSql: "('ACCEPTED')"),
                    DriverAssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArrivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RoutePolyline = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSOSActivated = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Trips__3214EC073D1A47E2", x => x.Id);
                    table.CheckConstraint("CK_Trips_TripStatus", "[TripStatus] IN ('ACCEPTED', 'DRIVER_ARRIVING', 'ARRIVED', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "FK_Trips_Bookings",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Trips_CancelledBy",
                        column: x => x.CancelledByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Trips_DriverProfiles",
                        column: x => x.DriverId,
                        principalTable: "DriverProfiles",
                        principalColumn: "DriverId");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransactionReference = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "VND"),
                    PaymentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValueSql: "('Pending')"),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Payments__3214EC076511D9DA", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Payments_PaymentMethod", "[PaymentMethod] IN ('QR', 'CASH')");
                    table.CheckConstraint("CK_Payments_PaymentStatus", "[PaymentStatus] IN ('Pending', 'Success', 'Failed', 'Cancelled')");
                    table.CheckConstraint("CK_Payments_TransactionReference", "([PaymentMethod] = 'QR' AND [TransactionReference] IS NOT NULL) OR ([PaymentMethod] = 'CASH' AND [TransactionReference] IS NULL)");
                    table.ForeignKey(
                        name: "FK_Payments_Trips",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Ratings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingScore = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Ratings__3214EC07E3A4C19E", x => x.Id);
                    table.CheckConstraint("CK_Ratings_Customer_Driver", "[CustomerId] <> [DriverId]");
                    table.CheckConstraint("CK_Ratings_RatingScore", "[RatingScore] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_Ratings_Customers",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Ratings_DriverProfiles",
                        column: x => x.DriverId,
                        principalTable: "DriverProfiles",
                        principalColumn: "DriverId");
                    table.ForeignKey(
                        name: "FK_Ratings_Trips",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValueSql: "('Pending')"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Reports__3214EC07D235B6F9", x => x.Id);
                    table.CheckConstraint("CK_Reports_Status", "[Status] IN ('Pending', 'Resolved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_Complaint_Trip",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Complaint_User",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RouteDeviations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    Location = table.Column<Point>(type: "geography", nullable: false),
                    DistanceDeviationMeters = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__RouteDev__3214EC07CBFC6AE6", x => x.Id);
                    table.CheckConstraint("CK_RouteDeviations_DistanceDeviationMeters", "[DistanceDeviationMeters] >= 0");
                    table.CheckConstraint("CK_RouteDeviations_Location", "[Location].STSrid = 4326");
                    table.ForeignKey(
                        name: "FK_RouteDeviations_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SOSAlerts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Location = table.Column<Point>(type: "geography", nullable: false),
                    EmergencyMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SOSStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValueSql: "('Active')"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SOSAlert__3214EC07C31DC631", x => x.Id);
                    table.CheckConstraint("CK_SOSAlerts_Location", "[Location].STSrid = 4326");
                    table.CheckConstraint("CK_SOSAlerts_SOSStatus", "[SOSStatus] IN ('Active', 'Resolved', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_SOSAlerts_ResolvedBy",
                        column: x => x.ResolvedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SOSAlerts_TriggeredBy",
                        column: x => x.TriggeredByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SOSAlerts_Trips",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TripShares",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    SharedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShareToken = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__TripShar__3214EC079FAF0E0E", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripShares_Recipient",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripShares_SharedBy",
                        column: x => x.SharedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripShares_Trips",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<long>(type: "bigint", nullable: false),
                    TripId = table.Column<long>(type: "bigint", nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__WalletTr__3214EC0740D81F8C", x => x.Id);
                    table.CheckConstraint("CK_WalletTransactions_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_WalletTransactions_TransactionType", "[TransactionType] IN ('Income', 'Withdrawal', 'Penalty', 'Bonus')");
                    table.ForeignKey(
                        name: "FK_WalletTransaction_Trip",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletTransaction_Wallet",
                        column: x => x.WalletId,
                        principalTable: "DriverWallets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingPromotions_PromotionId",
                table: "BookingPromotions",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CancelledBy",
                table: "Bookings",
                column: "CancelledBy");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CustomerId",
                table: "Bookings",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PricingRuleId",
                table: "Bookings",
                column: "PricingRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ServiceTypeId",
                table: "Bookings",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SurgePricingRuleId",
                table: "Bookings",
                column: "SurgePricingRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_VehicleId",
                table: "Bookings",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverKyc_DriverId",
                table: "DriverKyc",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverWallets_DriverId",
                table: "DriverWallets",
                column: "DriverId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TripId",
                table: "Payments",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_ServiceTypeId",
                table: "PricingRules",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_CustomerId",
                table: "Ratings",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_DriverId",
                table: "Ratings",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_TripId",
                table: "Ratings",
                column: "TripId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_TripId",
                table: "Reports",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_UserId",
                table: "Reports",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteDeviations_TripId",
                table: "RouteDeviations",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_SOSAlerts_ResolvedByUserId",
                table: "SOSAlerts",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SOSAlerts_TriggeredByUserId",
                table: "SOSAlerts",
                column: "TriggeredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SOSAlerts_TripId",
                table: "SOSAlerts",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_BookingId",
                table: "Trips",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_CancelledByUserId",
                table: "Trips",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DriverId",
                table: "Trips",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_TripShares_RecipientUserId",
                table: "TripShares",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripShares_SharedByUserId",
                table: "TripShares",
                column: "SharedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripShares_TripId",
                table: "TripShares",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_OwnerUserId",
                table: "Vehicles",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_TripId",
                table: "WalletTransactions",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_WalletId",
                table: "WalletTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_WalletId",
                table: "WithdrawalRequests",
                column: "WalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BookingPromotions");

            migrationBuilder.DropTable(
                name: "DriverKyc");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "RouteDeviations");

            migrationBuilder.DropTable(
                name: "SOSAlerts");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "TripShares");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "WithdrawalRequests");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Promotions");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropTable(
                name: "DriverWallets");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "DriverProfiles");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "PricingRules");

            migrationBuilder.DropTable(
                name: "SurgePricingRules");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "ServiceTypes");
        }
    }
}
