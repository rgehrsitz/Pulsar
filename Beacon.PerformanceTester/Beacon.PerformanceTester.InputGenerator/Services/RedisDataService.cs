using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Beacon.PerformanceTester.InputGenerator.Services
{
    /// <summary>
    /// Service for interacting with Redis data
    /// </summary>
    public class RedisDataService : IRedisDataService, IDisposable
    {
        private readonly ILogger<RedisDataService> _logger;
        private readonly IConfiguration _configuration;
        private ConnectionMultiplexer? _redis;
        private IDatabase? _db;
        private bool _disposed = false;

        public RedisDataService(ILogger<RedisDataService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Initialize the Redis connection
        /// </summary>
        public async Task InitializeAsync()
        {
            string redisConnection = _configuration["Redis:ConnectionString"] ?? "localhost:6379";
            _logger.LogInformation("Connecting to Redis at {Connection}", redisConnection);

            try
            {
                var options = ConfigurationOptions.Parse(redisConnection);
                options.AbortOnConnectFail = false;
                _redis = await ConnectionMultiplexer.ConnectAsync(options);
                _db = _redis.GetDatabase();
                _logger.LogInformation("Connected to Redis successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Redis");
                throw;
            }
        }

        /// <summary>
        /// Set a value in Redis with timestamp
        /// </summary>
        public async Task SetValueAsync(string key, double value, long? timestampMs = null)
        {
            if (_db == null)
            {
                await InitializeAsync();
            }

            try
            {
                // Calculate timestamp if not provided
                long timestamp = timestampMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // First, delete the key to avoid WRONGTYPE errors
                await _db!.KeyDeleteAsync(key);

                // Set both Redis hash and string for compatibility with different consumers

                // Hash format (used by Beacon)
                await _db!.HashSetAsync(
                    key,
                    new HashEntry[]
                    {
                        new HashEntry("value", value.ToString()),
                        new HashEntry("timestamp", timestamp.ToString()),
                    }
                );

                // String format (simple value storage)
                // We'll use a separate string key to avoid conflicts
                string stringKey = $"{key}:string";
                await _db.StringSetAsync(stringKey, value.ToString());

                _logger.LogDebug(
                    "Set {Key} = {Value} with timestamp {Timestamp}",
                    key,
                    value,
                    timestamp
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value for {Key}, attempting recovery", key);

                try
                {
                    // Try to delete the key and retry with just the string format
                    await _db!.KeyDeleteAsync(key);
                    string stringKey = $"{key}:string";
                    await _db.StringSetAsync(stringKey, value.ToString());
                    _logger.LogInformation("Successfully set {Key} using string format only", key);
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(
                        retryEx,
                        "Failed to recover from error when setting {Key}",
                        key
                    );
                    throw;
                }
            }
        }

        /// <summary>
        /// Clear test data from Redis
        /// </summary>
        public async Task ClearTestDataAsync()
        {
            if (_db == null)
            {
                await InitializeAsync();
            }

            try
            {
                if (_redis == null)
                {
                    _logger.LogWarning("Redis connection not initialized");
                    return;
                }

                // Get all keys with our prefixes
                var server = _redis.GetServer(_redis.GetEndPoints().First());

                // Process keys in batches to avoid large memory allocation
                List<RedisKey> allKeys = new List<RedisKey>();

                // Get input keys
                await foreach (var key in server.KeysAsync(pattern: "input:*"))
                {
                    allKeys.Add(key);
                }

                // Get output keys
                await foreach (var key in server.KeysAsync(pattern: "output:*"))
                {
                    allKeys.Add(key);
                }

                // Get buffer keys
                await foreach (var key in server.KeysAsync(pattern: "buffer:*"))
                {
                    allKeys.Add(key);
                }

                if (allKeys.Count > 0)
                {
                    await _db!.KeyDeleteAsync(allKeys.ToArray());
                    _logger.LogInformation("Cleared {Count} Redis keys", allKeys.Count);
                }
                else
                {
                    _logger.LogInformation("No test data keys found to clear");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing test data");
                throw;
            }
        }

        /// <summary>
        /// Get a value from Redis
        /// </summary>
        public async Task<double?> GetValueAsync(string key)
        {
            if (_db == null)
            {
                await InitializeAsync();
            }

            try
            {
                // Try hash format first
                var hashValue = await _db!.HashGetAsync(key, "value");
                if (!hashValue.IsNull)
                {
                    if (double.TryParse(hashValue.ToString(), out double value))
                    {
                        return value;
                    }
                }

                // Try string key directly
                var stringValue = await _db.StringGetAsync(key);
                if (!stringValue.IsNull)
                {
                    if (double.TryParse(stringValue.ToString(), out double value))
                    {
                        return value;
                    }
                }

                // Try also the separate string key format
                string stringKey = $"{key}:string";
                var separateStringValue = await _db.StringGetAsync(stringKey);
                if (!separateStringValue.IsNull)
                {
                    if (double.TryParse(separateStringValue.ToString(), out double value))
                    {
                        return value;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting value for {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Check if a key exists in Redis
        /// </summary>
        public async Task<bool> HasValueAsync(string key)
        {
            if (_db == null)
            {
                await InitializeAsync();
            }

            try
            {
                return await _db!.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if key exists: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Close the Redis connection
        /// </summary>
        public async Task CloseAsync()
        {
            if (_redis != null)
            {
                _logger.LogInformation("Closing Redis connection");
                _redis.Close();
                _redis.Dispose();
                _redis = null;
                _db = null;
            }
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _redis?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
