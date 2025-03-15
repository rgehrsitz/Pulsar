// File: Pulsar.Runtime/Services/RedisService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using StackExchange.Redis;

namespace Beacon.Runtime.Services
{
    public class RedisService : IRedisService, IDisposable
    {
        private readonly ILogger<RedisService> _logger;
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly RedisConfiguration _config;
        private readonly string _keyDelimiter;

        public RedisService(RedisConfiguration config, ILogger<RedisService> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _keyDelimiter = _config.KeyDelimiter; // Store the delimiter for use in key construction

            _retryPolicy = Policy
                .Handle<RedisConnectionException>()
                .Or<RedisTimeoutException>()
                .WaitAndRetryAsync(
                    _config.RetryCount,
                    retryAttempt =>
                        TimeSpan.FromMilliseconds(
                            _config.RetryBaseDelayMs * Math.Pow(2, retryAttempt - 1)
                        ),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            exception,
                            "Redis operation failed. Retry attempt {RetryCount} after {RetryDelay}ms",
                            retryCount,
                            timeSpan.TotalMilliseconds
                        );
                    }
                );

            _redis = ConnectionMultiplexer.Connect(config.ToRedisOptions());
            _db = _redis.GetDatabase();
        }

        public async Task<Dictionary<string, double>> GetSensorValuesAsync(IEnumerable<string> sensorNames)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var result = new Dictionary<string, double>();
                
                foreach (var sensor in sensorNames)
                {
                    var value = await _db.StringGetAsync($"input{_keyDelimiter}{sensor}");
                    if (value.HasValue && double.TryParse(value.ToString(), out var doubleValue))
                    {
                        result[sensor] = doubleValue;
                    }
                }
                
                return result;
            });
        }

        public async Task SetOutputValuesAsync(Dictionary<string, double> outputs)
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                foreach (var (key, value) in outputs)
                {
                    await _db.StringSetAsync($"output{_keyDelimiter}{key}", value.ToString());
                }
                return true;
            });
        }

        public async Task<Dictionary<string, object>> GetAllInputsAsync()
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var result = new Dictionary<string, object>();
                var server = _redis.GetServer(_redis.GetEndPoints()[0]);
                var keys = server.Keys(pattern: $"input{_keyDelimiter}*");

                foreach (var key in keys)
                {
                    var value = await _db.StringGetAsync(key);
                    if (value.HasValue)
                    {
                        var keyString = key.ToString();
                        var sensorName = keyString.Substring(keyString.IndexOf(_keyDelimiter) + _keyDelimiter.Length);
                        result[sensorName] = value.ToString();
                    }
                }

                return result;
            });
        }

        public async Task SetOutputsAsync(Dictionary<string, object> outputs)
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                foreach (var (key, value) in outputs)
                {
                    await _db.StringSetAsync($"output{_keyDelimiter}{key}", value.ToString());
                }
                return true;
            });
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                await _db.PingAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis health check failed");
                return false;
            }
        }

        public void Dispose()
        {
            _redis?.Dispose();
        }
    }
}
