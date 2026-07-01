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

        Task<long> IncrementAsync(string key, TimeSpan expiration);

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
