// File: Pulsar.Compiler/Config/Templates/Runtime/Services/RedisHealthCheck.cs
// Version: 1.0.0

using System;
using System.Threading;
using System.Threading.Tasks;
using Beacon.Runtime.Models;
using Serilog;
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
        private readonly RedisService _redisService;
        private bool _isHealthy = true;
        private bool _disposed;

        /// <summary>
        /// Whether the Redis service is healthy
        /// </summary>
        public bool IsHealthy => _isHealthy;

        /// <summary>
        /// Creates a new Redis health check
        /// </summary>
        public RedisHealthCheck(RedisService redisService, ILogger logger)
        {
            _redisService = redisService;
            _logger = logger.ForContext<RedisHealthCheck>();
            _config = new RedisConfiguration(); // Default configuration

            // Start health check timer (every 30 seconds)
            _healthCheckTimer = new Timer(
                CheckHealth,
                null,
                TimeSpan.FromSeconds(5), // Initial delay
                TimeSpan.FromSeconds(30) // Interval
            );

            _logger.Debug("Redis health check initialized");
        }

        private void CheckHealth(object? state)
        {
            try
            {
                // Perform health check
                var wasHealthy = _isHealthy;
                _isHealthy = _redisService.IsHealthy;

                // Log status changes
                if (wasHealthy && !_isHealthy)
                {
                    _logger?.Warning("Redis service is now unhealthy");
                }
                else if (!wasHealthy && _isHealthy)
                {
                    _logger?.Information("Redis service is now healthy");
                }

                _logger?.Debug("Redis health check completed: {IsHealthy}", _isHealthy);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error performing Redis health check");
                _isHealthy = false;
            }
        }

        /// <summary>
        /// Disposes the health check
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _healthCheckTimer?.Dispose();
            _disposed = true;
        }
    }
}
