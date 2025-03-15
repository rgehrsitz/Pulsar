using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Beacon.PerformanceTester.OutputMonitor.Services
{
    /// <summary>
    /// Implementation of the Redis monitor service
    /// </summary>
    public class RedisMonitorService : IRedisMonitorService, IDisposable
    {
        private readonly ILogger<RedisMonitorService> _logger;
        private readonly IConfiguration _configuration;
        private ConnectionMultiplexer? _redis;
        private IDatabase? _db;
        private ISubscriber? _subscriber;
        private readonly ConcurrentDictionary<
            string,
            (Func<string, string, long, Task> Callback, bool IsActive)
        > _keyMonitors = new();
        private bool _disposed = false;

        public RedisMonitorService(
            ILogger<RedisMonitorService> logger,
            IConfiguration configuration
        )
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
                _subscriber = _redis.GetSubscriber();
                _logger.LogInformation("Connected to Redis successfully");

                // Setup keyspace notifications
                var server = _redis.GetServer(_redis.GetEndPoints()[0]);
                await server.ExecuteAsync("CONFIG", "SET", "notify-keyspace-events", "KEA");
                _logger.LogInformation("Configured Redis keyspace notifications");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Redis");
                throw;
            }
        }

        /// <summary>
        /// Monitor a specific Redis key for changes
        /// </summary>
        public async Task MonitorKeyAsync(
            string key,
            Func<string, string, long, Task> callback,
            CancellationToken cancellationToken
        )
        {
            if (_subscriber == null)
            {
                await InitializeAsync();
            }

            try
            {
                // Store the callback for this key
                _keyMonitors[key] = (callback, true);

                // Setup subscription to keyspace events for this key
                string channel = $"__keyspace@0__:{key}";
                await _subscriber!.SubscribeAsync(
                    channel,
                    async (redisChannel, value) =>
                    {
                        if (
                            cancellationToken.IsCancellationRequested
                            || !_keyMonitors.TryGetValue(key, out var monitor)
                            || !monitor.IsActive
                        )
                        {
                            return;
                        }

                        try
                        {
                            // Get the current value and timestamp
                            var result = await GetValueWithTimestampAsync(key);
                            if (result.HasValue)
                            {
                                // Call the callback with the new value
                                await monitor.Callback(
                                    key,
                                    result.Value.Value,
                                    result.Value.Timestamp
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Error processing Redis key change for {Key}",
                                key
                            );
                        }
                    }
                );

                _logger.LogInformation("Started monitoring Redis key {Key}", key);

                // Check if value already exists
                var currentValue = await GetValueWithTimestampAsync(key);
                if (currentValue.HasValue)
                {
                    await callback(key, currentValue.Value.Value, currentValue.Value.Timestamp);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up Redis key monitor for {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Get the current value and timestamp for a key
        /// </summary>
        public async Task<(string Value, long Timestamp)?> GetValueWithTimestampAsync(string key)
        {
            if (_db == null)
            {
                await InitializeAsync();
            }

            try
            {
                // Try hash format first (Beacon format)
                var hashValue = await _db!.HashGetAsync(key, "value");
                var hashTimestamp = await _db!.HashGetAsync(key, "timestamp");

                if (!hashValue.IsNull && !hashTimestamp.IsNull)
                {
                    if (long.TryParse(hashTimestamp.ToString(), out long timestamp))
                    {
                        return (hashValue.ToString()!, timestamp);
                    }
                }

                // Try string format as fallback
                var stringValue = await _db.StringGetAsync(key);
                if (!stringValue.IsNull)
                {
                    // For string format, we don't have timestamp, so use current time
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    return (stringValue.ToString()!, now);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting value with timestamp for {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Close Redis connections
        /// </summary>
        public async Task CloseAsync()
        {
            foreach (var key in _keyMonitors.Keys)
            {
                if (_keyMonitors.TryGetValue(key, out var monitor))
                {
                    _keyMonitors[key] = (monitor.Callback, false);
                }
            }

            if (_redis != null)
            {
                _logger.LogInformation("Closing Redis connection");

                // Unsubscribe from all channels
                if (_subscriber != null)
                {
                    await _subscriber.UnsubscribeAllAsync();
                }

                _redis.Close();
                _redis.Dispose();
                _redis = null;
                _db = null;
                _subscriber = null;
            }
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
