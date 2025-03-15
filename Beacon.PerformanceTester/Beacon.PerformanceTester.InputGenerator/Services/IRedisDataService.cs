using System;
using System.Threading.Tasks;

namespace Beacon.PerformanceTester.InputGenerator.Services
{
    /// <summary>
    /// Interface for Redis data operations
    /// </summary>
    public interface IRedisDataService
    {
        /// <summary>
        /// Initialize Redis connection
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Set a value in Redis with proper timestamp
        /// </summary>
        /// <param name="key">Redis key</param>
        /// <param name="value">Value to set</param>
        /// <param name="timestampMs">Optional timestamp (will use current time if not provided)</param>
        Task SetValueAsync(string key, double value, long? timestampMs = null);

        /// <summary>
        /// Clear all test data from Redis
        /// </summary>
        Task ClearTestDataAsync();

        /// <summary>
        /// Get a value from Redis
        /// </summary>
        /// <param name="key">Redis key</param>
        /// <returns>Value or null if not found</returns>
        Task<double?> GetValueAsync(string key);

        /// <summary>
        /// Check if Redis has a value for a key
        /// </summary>
        /// <param name="key">Redis key</param>
        /// <returns>True if key exists</returns>
        Task<bool> HasValueAsync(string key);

        /// <summary>
        /// Close the Redis connection
        /// </summary>
        Task CloseAsync();
    }
}
