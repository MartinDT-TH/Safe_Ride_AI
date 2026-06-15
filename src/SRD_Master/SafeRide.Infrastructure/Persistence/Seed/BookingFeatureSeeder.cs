// using Microsoft.AspNetCore.Identity;
// using Microsoft.EntityFrameworkCore;
// using NetTopologySuite.Geometries;
// using SafeRide.Domain.Entities;
// using SafeRide.Domain.Enums;
// using SafeRide.Infrastructure.Persistence;

// namespace SafeRide.Infrastructure.Persistence.Seeders;

// public static class BookingFeatureSeeder
// {
//     private static readonly Guid CustomerId = Guid.Parse("8d3e426f-a485-4d72-981d-4056a13c8387");
//     private static readonly Guid DriverId = Guid.Parse("11111111-1111-1111-1111-111111111111");

//     public static async Task SeedAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
//     {
//         var now = DateTime.UtcNow;

//         await SeedCustomerAsync(db, now, cancellationToken);
//         await SeedDriverAsync(db, now, cancellationToken);

//         var perTripServiceType = await SeedServiceTypeAsync(db, "PerTrip", cancellationToken);
//         var hourlyServiceType = await SeedServiceTypeAsync(db, "Hourly", cancellationToken);

//         var vehicle = await SeedCustomerVehicleAsync(db, now, cancellationToken);

//         var perTripPricingRule = await SeedPricingRuleAsync(
//             db,
//             vehicleClass: "B",
//             serviceTypeId: perTripServiceType.Id,
//             baseFare: 30000,
//             minFare: 70000,
//             pricePerKm: 12000,
//             pricePerHour: null,
//             now,
//             cancellationToken
//         );

//         await SeedPricingRuleAsync(
//             db,
//             vehicleClass: "B",
//             serviceTypeId: hourlyServiceType.Id,
//             baseFare: 50000,
//             minFare: 100000,
//             pricePerKm: null,
//             pricePerHour: 90000,
//             now,
//             cancellationToken
//         );

//         await SeedPromotionAsync(db, now, cancellationToken);

//         await SeedSampleBookingAsync(
//             db,
//             customerId: CustomerId,
//             vehicleId: vehicle.Id,
//             serviceTypeId: perTripServiceType.Id,
//             pricingRuleId: perTripPricingRule.Id,
//             now,
//             cancellationToken
//         );

//         await db.SaveChangesAsync(cancellationToken);
//     }

//     private static async Task SeedCustomerAsync(
//         ApplicationDbContext db,
//         DateTime now,
//         CancellationToken cancellationToken)
//     {
//         var exists = await db.Users.AnyAsync(x => x.Id == CustomerId, cancellationToken);

//         if (exists)
//         {
//             return;
//         }

//         db.Users.Add(new IdentityUser
//         {
//             Id = CustomerId,
//             UserName = "customer.test@srd.com",
//             NormalizedUserName = "CUSTOMER.TEST@SRD.COM",
//             Email = "customer.test@srd.com",
//             NormalizedEmail = "CUSTOMER.TEST@SRD.COM",
//             EmailConfirmed = true,
//             PhoneNumber = "0900000000",
//             PhoneNumberConfirmed = true,

//             TwoFactorEnabled = false,
//             LockoutEnabled = true,
//             AccessFailedCount = 0,

//             SecurityStamp = Guid.NewGuid().ToString(),
//             ConcurrencyStamp = Guid.NewGuid().ToString(),

//             FullName = "Khách hàng Test Booking",
//             AvatarUrl = null,
//             IsActive = true,
//             BanReason = null,
//             Gender = "Male",
//             DateOfBirth = new DateTime(2000, 1, 1),

//             CreatedAt = now,
//             UpdatedAt = now
//         });
//     }

//     private static async Task SeedDriverAsync(
//         ApplicationDbContext db,
//         DateTime now,
//         CancellationToken cancellationToken)
//     {
//         var driverExists = await db.Users.AnyAsync(x => x.Id == DriverId, cancellationToken);

//         if (!driverExists)
//         {
//             db.Users.Add(new IdentityUser
//             {
//                 Id = DriverId,
//                 UserName = "driver.test@srd.com",
//                 NormalizedUserName = "DRIVER.TEST@SRD.COM",
//                 Email = "driver.test@srd.com",
//                 NormalizedEmail = "DRIVER.TEST@SRD.COM",
//                 EmailConfirmed = true,
//                 PhoneNumber = "0900000001",
//                 PhoneNumberConfirmed = true,

//                 TwoFactorEnabled = false,
//                 LockoutEnabled = true,
//                 AccessFailedCount = 0,

//                 SecurityStamp = Guid.NewGuid().ToString(),
//                 ConcurrencyStamp = Guid.NewGuid().ToString(),

//                 FullName = "Tài xế Test Booking",
//                 AvatarUrl = null,
//                 IsActive = true,
//                 BanReason = null,
//                 Gender = "Male",
//                 DateOfBirth = new DateTime(1995, 1, 1),

//                 CreatedAt = now,
//                 UpdatedAt = now
//             });

//             await db.SaveChangesAsync(cancellationToken);
//         }

//         var driverProfile = await db.DriverProfiles
//             .FirstOrDefaultAsync(x => x.DriverId == DriverId, cancellationToken);

//         if (driverProfile is null)
//         {
//             db.DriverProfiles.Add(new DriverProfile
//             {
//                 DriverId = DriverId,
//                 IdentityCardNumber = "012345678901",
//                 ExperienceYears = 3,
//                 HomeAddress = "Đà Nẵng",
//                 WorkStatus = "Online",
//                 LastActiveAt = now,
//                 CreatedAt = now,
//                 UpdatedAt = now
//             });
//         }
//         else
//         {
//             driverProfile.WorkStatus = "Online";
//             driverProfile.LastActiveAt = now;
//             driverProfile.UpdatedAt = now;
//         }

//         var hasDrivingLicense = await db.DriverKyc
//             .AnyAsync(x => x.DriverId == DriverId && x.DocumentType == "DRIVING_LICENSE", cancellationToken);

//         if (!hasDrivingLicense)
//         {
//             db.DriverKyc.Add(new DriverKyc
//             {
//                 DriverId = DriverId,
//                 DocumentType = DriverKycDocumentType.DRIVING_LICENSE,
//                 DocumentNumber = "GPLX-B-000001",
//                 LicenseClass = RequiredLicenseClass.B,

//                 FrontImageUrl = "https://example.com/mock-license-front.jpg",
//                 BackImageUrl = "https://example.com/mock-license-back.jpg",
//                 FileUrl = null,

//                 IssueDate = DateTime.UtcNow.AddYears(-2).Date,
//                 ExpiryDate = DateTime.UtcNow.AddYears(8).Date,

//                 KycStatus = KycStatus.Approved,
//                 CreatedAt = now,
//                 VerifiedAt = now,
//                 RejectionReason = null
//             });
//         }

//         var hasWallet = await db.DriverWallets
//             .AnyAsync(x => x.DriverId == DriverId, cancellationToken);

//         if (!hasWallet)
//         {
//             db.DriverWallets.Add(new DriverWallet
//             {
//                 DriverId = DriverId,
//                 CurrentBalance = 0
//             });
//         }
//     }

//     private static async Task<ServiceType> SeedServiceTypeAsync(
//         ApplicationDbContext db,
//         string serviceName,
//         CancellationToken cancellationToken)
//     {
//         var serviceType = await db.ServiceTypes
//             .FirstOrDefaultAsync(x => x.ServiceName == serviceName, cancellationToken);

//         if (serviceType is not null)
//         {
//             return serviceType;
//         }

//         serviceType = new ServiceType
//         {
//             ServiceName = serviceName
//         };

//         db.ServiceTypes.Add(serviceType);
//         await db.SaveChangesAsync(cancellationToken);

//         return serviceType;
//     }

//     private static async Task<Vehicle> SeedCustomerVehicleAsync(
//         ApplicationDbContext db,
//         DateTime now,
//         CancellationToken cancellationToken)
//     {
//         const string plateNumber = "43A-88888";

//         var vehicle = await db.Vehicles
//             .FirstOrDefaultAsync(x => x.PlateNumber == plateNumber, cancellationToken);

//         if (vehicle is not null)
//         {
//             return vehicle;
//         }

//         vehicle = new Vehicle
//         {
//             OwnerUserId = CustomerId,
//             PlateNumber = plateNumber,
//             BrandModel = "Toyota Vios",
//             RequiredLicenseClass = RequiredLicenseClass.B,
//             VehicleType = VehicleType.Car,
//             EngineType = EngineType.ICE,
//             TransmissionType = TransmissionType.Automatic,
//             Color = "Trắng",
//             IsDeleted = false,
//             CreatedAt = now
//         };

//         db.Vehicles.Add(vehicle);
//         await db.SaveChangesAsync(cancellationToken);

//         return vehicle;
//     }

//     private static async Task<PricingRule> SeedPricingRuleAsync(
//         ApplicationDbContext db,
//         string vehicleClass,
//         long serviceTypeId,
//         decimal baseFare,
//         decimal minFare,
//         decimal? pricePerKm,
//         decimal? pricePerHour,
//         DateTime now,
//         CancellationToken cancellationToken)
//     {
//         var pricingRule = await db.PricingRules
//             .FirstOrDefaultAsync(
//                 x => x.VehicleClass == vehicleClass
//                      && x.ServiceTypeId == serviceTypeId
//                      && x.IsActive,
//                 cancellationToken);

//         if (pricingRule is not null)
//         {
//             return pricingRule;
//         }

//         pricingRule = new PricingRule
//         {
//             VehicleClass = VehicleClass,
//             ServiceTypeId = serviceTypeId,
//             BaseFare = baseFare,
//             MinFare = minFare,
//             PricePerKm = pricePerKm,
//             PricePerHour = pricePerHour,
//             IsActive = true,
//             CreatedAt = now,
//             UpdatedAt = now
//         };

//         db.PricingRules.Add(pricingRule);
//         await db.SaveChangesAsync(cancellationToken);

//         return pricingRule;
//     }

//     private static async Task SeedPromotionAsync(
//         ApplicationDbContext db,
//         DateTime now,
//         CancellationToken cancellationToken)
//     {
//         var exists = await db.Promotions
//             .AnyAsync(x => x.PromotionCode == "SAFERIDE20", cancellationToken);

//         if (exists)
//         {
//             return;
//         }

//         db.Promotions.Add(new Promotion
//         {
//             PromotionCode = "SAFERIDE20",
//             DiscountType = DiscountType.Percentage,
//             DiscountValue = 20,
//             StartDate = now,
//             EndDate = now.AddDays(30),
//             MaxUsageCount = 100,
//             CurrentUsageCount = 0,
//             MinimumOrderValue = 50000,
//             MaximumDiscountValue = 30000,
//             UsageLimitPerUser = 1,
//             IsActive = true
//         });
//     }

//     private static async Task SeedSampleBookingAsync(
//         ApplicationDbContext db,
//         Guid customerId,
//         long vehicleId,
//         long serviceTypeId,
//         long pricingRuleId,
//         DateTime now,
//         CancellationToken cancellationToken)
//     {
//         var hasSampleBooking = await db.Bookings.AnyAsync(
//             x => x.CustomerId == customerId
//                  && x.VehicleId == vehicleId
//                  && x.BookingStatus == BookingStatus.SEARCHING_DRIVER,
//             cancellationToken);

//         if (hasSampleBooking)
//         {
//             return;
//         }

//         db.Bookings.Add(new Booking
//         {
//             CustomerId = customerId,
//             VehicleId = vehicleId,
//             ServiceTypeId = serviceTypeId,

//             BookingType = BookingType.Now,
//             BookingStatus = BookingStatus.SEARCHING_DRIVER,
//             BookingSource = BookingSource.Manual,
//             ScheduledAt = null,

//             PickupAddress = "FPT University Đà Nẵng",
//             PickupLocation = new Point(108.2520, 15.9759)
//             {
//                 SRID = 4326
//             },

//             DestinationAddress = "Cầu Rồng Đà Nẵng",
//             DestinationLocation = new Point(108.2278, 16.0611)
//             {
//                 SRID = 4326
//             },

//             EstimatedDistanceKm = 10.50m,
//             EstimatedDurationMinutes = 25,
//             EstimatedFare = 156000,

//             SpecialRequest = "Tài xế vui lòng gọi trước khi đến",

//             PricingRuleId = pricingRuleId,
//             SurgePricingRuleId = null,

//             CancelledBy = null,
//             CancellationReason = null,

//             CreatedAt = now,
//             UpdatedAt = now
//         });
//     }
// }