﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafeRide.Infrastructure.Redis
{
    public interface IRedisService
    {
        Task SetAsync(
            string key,
            string value,
            TimeSpan expiration);

        Task SetPersistentAsync(string key, string value) =>
            Task.FromException(
                new NotSupportedException("Persistent Redis values are not supported."));

        Task<bool> SetIfNotExistsAsync(
            string key,
            string value,
            TimeSpan expiration);

        Task<bool> TryAcquireDistributedLockAsync(
            string key,
            string value,
            TimeSpan expiration);

        Task<string?> GetAsync(string key);

        Task<IReadOnlyDictionary<string, string?>> GetManyAsync(
            IReadOnlyCollection<string> keys);

        Task RemoveAsync(string key);

        Task ExpireAsync(
            string key,
            TimeSpan expiration,
            CancellationToken cancellationToken = default);

        Task ListRightPushTrimAndExpireAsync(
            string key,
            string value,
            int maxLength,
            TimeSpan expiration,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> ListRangeAsync(
            string key,
            long start = 0,
            long stop = -1,
            CancellationToken cancellationToken = default);

        Task<long> IncrementAsync(string key, TimeSpan expiration);

        async Task<double> SetMaximumDoubleAsync(
            string key,
            double candidate,
            TimeSpan expiration)
        {
            var currentValue = await GetAsync(key);
            var current = double.TryParse(
                currentValue,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : 0d;
            var maximum = Math.Max(current, candidate);
            await SetAsync(
                key,
                maximum.ToString(
                    "R",
                    System.Globalization.CultureInfo.InvariantCulture),
                expiration);
            return maximum;
        }

        Task GeoAddAsync(
            string key,
            double longitude,
            double latitude,
            string member);

        Task GeoRemoveAsync(
            string key,
            string member,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GeoRadiusAsync(
            string key,
            double longitude,
            double latitude,
            double radiusKm,
            int count);

        Task<OtpVerificationResult> VerifyAndConsumeOtpAsync(
            string otpKey,
            string attemptsKey,
            string expectedHash,
            int maxAttempts);

        Task<TripTrackingUpdateResult> RecordTripTrackingPointAsync(
            TripTrackingPoint point,
            TripTrackingWriteOptions options,
            CancellationToken cancellationToken = default);

        Task<TripTrackingSnapshot> GetTripTrackingSnapshotAsync(
            long tripId,
            CancellationToken cancellationToken = default);

        Task RemoveTripTrackingAsync(
            long tripId,
            CancellationToken cancellationToken = default);
    }
}

namespace SafeRide.Infrastructure.Redis
{
    public enum OtpVerificationResult
    {
        Success,
        Missing,
        Invalid,
        AttemptsExceeded
    }
}
