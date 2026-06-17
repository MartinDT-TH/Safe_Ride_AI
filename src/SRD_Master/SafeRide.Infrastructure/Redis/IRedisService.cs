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

        Task<string?> GetAsync(string key);

        Task RemoveAsync(string key);

        Task<long> IncrementAsync(string key, TimeSpan expiration);

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