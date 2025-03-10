// File: Pulsar.Compiler/Config/Templates/Runtime/Services/RedisHealthCheck.cs
// Version: 1.0.0

using System;
using System.Threading;
using System.Threading.Tasks;
using Beacon.Runtime.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Beacon.Runtime.Services
{
    /// <summary>
    /// Health check for Redis connections
    /// </summary>
    public class RedisHealthCheck : IDisposable
    {
        private readonly ILogger? _logger;
        private readonly RedisConfiguration _config;
        private readonly Timer? _healthCheckTimer;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
        private bool _isHealthy = false;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private string _lastErrorMessage = string.Empty;

        /// <summary>
        /// Gets a value indicating whether Redis is healthy
        /// </summary>
        public bool IsHealthy => _isHealthy;

        /// <summary>
        /// Gets the last time a health check was performed
        /// </summary>
        public DateTime LastCheckTime => _lastCheckTime;

        /// <summary>
        /// Gets the last error message if Redis is unhealthy
        /// </summary>
        public string LastErrorMessage => _lastErrorMessage;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisHealthCheck"/> class with just configuration
        /// </summary>
        /// <param name="config">Redis configuration</param>
        public RedisHealthCheck(RedisConfiguration config)
        {
            _config = config;
            _isHealthy = true; // Assume healthy until proven otherwise
            
            // Don't start timer in this constructor to avoid circular dependency
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisHealthCheck"/> class
        /// </summary>
        /// <param name="config">Redis configuration</param>
        /// <param name="logger">Logger</param>
        public RedisHealthCheck(RedisConfiguration config, ILogger logger)
        {
            _config = config;
            _logger = logger;
            _isHealthy = true; // Assume healthy until proven otherwise

            // Start health check timer
            _healthCheckTimer = new Timer(CheckHealth, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        private async void CheckHealth(object? state)
        {
            await CheckHealthAsync(_config, _logger);
        }

        /// <summary>
        /// Performs a health check on the Redis connection
        /// </summary>
        /// <returns>True if Redis is healthy, false otherwise</returns>
        public async Task<bool> CheckHealthAsync(RedisConfiguration config, ILogger? logger = null)
        {
            try
            {
                _lastCheckTime = DateTime.UtcNow;
                
                // Create a temporary connection for health check
                using var connection = await ConnectionMultiplexer.ConnectAsync(config.ToRedisOptions());
                var db = connection.GetDatabase();
                
                // Ping the server
                var pingResult = await db.PingAsync();
                _isHealthy = pingResult != TimeSpan.MaxValue;
                
                if (_isHealthy)
                {
                    _lastErrorMessage = string.Empty;
                    logger?.LogDebug("Redis health check successful. Ping time: {PingTime}ms", pingResult.TotalMilliseconds);
                }
                else
                {
                    _lastErrorMessage = "Redis ping timeout";
                    logger?.LogWarning("Redis health check failed. Ping timeout.");
                }
                
                return _isHealthy;
            }
            catch (Exception ex)
            {
                _isHealthy = false;
                _lastErrorMessage = ex.Message;
                logger?.LogError(ex, "Redis health check failed");
                return false;
            }
        }

        public void Dispose()
        {
            if (_healthCheckTimer != null)
            {
                _healthCheckTimer.Dispose();
            }
        }
    }
}
