using System;
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
    }
}
