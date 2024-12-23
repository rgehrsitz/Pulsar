// Filename: RedisSensorDataProvider.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using Serilog;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Services;

namespace Pulsar.Runtime.Storage
{
    /// <summary>
    /// Provides sensor data access using Redis as the backend store
    /// </summary>
    public class RedisSensorDataProvider : ISensorDataProvider
    {
        private readonly IConnectionMultiplexer _connection;
        private readonly ILogger _logger;
        private readonly string _keyPrefix;
        private readonly SemaphoreSlim _reconnectLock = new SemaphoreSlim(1, 1);
        private readonly ISensorTemporalBufferService _temporalBuffer;

        private IDatabase? _db; // We lazily acquire and test the DB connection
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 1000;

        public RedisSensorDataProvider(
            IConnectionMultiplexer connection,
            ILogger logger,
            ISensorTemporalBufferService temporalBuffer,
            string keyPrefix = "sensor:"
        )
        {
            _connection = connection;
            _logger = logger.ForContext<RedisSensorDataProvider>();
            _temporalBuffer = temporalBuffer;
            _keyPrefix = keyPrefix;
        }

        /// <summary>
        /// Returns a healthy <see cref="IDatabase"/>. If the existing one fails ping, it reconnects.
        /// </summary>
        private async Task<IDatabase> GetDatabaseAsync()
        {
            if (_db != null)
            {
                try
                {
                    // Quick test of the connection
                    await _db.PingAsync();
                    return _db;
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Redis connection test failed, attempting to reconnect");
                    _db = null; // Force reconnection
                }
            }

            await _reconnectLock.WaitAsync();
            try
            {
                if (_db == null)
                {
                    // Acquire a fresh database from the shared multiplexer
                    _db = _connection.GetDatabase();
                    _logger.Information("Successfully connected to Redis");
                }
                return _db;
            }
            finally
            {
                _reconnectLock.Release();
            }
        }

        /// <summary>
        /// Wraps any Redis operation in retry logic for connection failures.
        /// </summary>
        private async Task<T> ExecuteWithRetryAsync<T>(
            Func<IDatabase, Task<T>> operation,
            string operationName
        )
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var db = await GetDatabaseAsync();
                    return await operation(db);
                }
                catch (RedisConnectionException ex)
                {
                    _logger.Warning(
                        ex,
                        "Redis connection error during {Operation} (attempt {Attempt}/{MaxRetries})",
                        operationName,
                        attempt,
                        MaxRetries
                    );

                    if (attempt == MaxRetries)
                        throw; // Re-throw on final attempt

                    // Exponential-ish backoff
                    await Task.Delay(RetryDelayMs * attempt);
                    _db = null; // Force reconnection on next attempt
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error during {Operation}", operationName);
                    throw;
                }
            }

            throw new InvalidOperationException(
                $"Failed to execute {operationName} after {MaxRetries} attempts"
            );
        }

        public async Task<IDictionary<string, double>> GetCurrentDataAsync()
        {
            return await ExecuteWithRetryAsync(
                async db =>
                {
                    var result = new Dictionary<string, double>();
                    var keys = await GetAllSensorKeysAsync(db);

                    if (!keys.Any())
                    {
                        return result;
                    }

                    var values = await db.StringGetAsync(keys.ToArray());
                    for (var i = 0; i < keys.Length; i++)
                    {
                        var key = keys[i];
                        var redisValue = values[i];

                        if (!redisValue.HasValue)
                        {
                            continue;
                        }

                        if (double.TryParse(redisValue.ToString(), out var doubleValue))
                        {
                            // Strip off our prefix to get the sensor name
                            var sensorName = key.ToString().Substring(_keyPrefix.Length);
                            result[sensorName] = doubleValue;
                        }
                        else
                        {
                            _logger.Warning(
                                "Invalid value format for key {Key}: {Value}",
                                key,
                                redisValue
                            );
                        }
                    }

                    return result;
                },
                "GetCurrentData"
            );
        }

        public async Task<IReadOnlyList<(DateTime Timestamp, double Value)>> GetHistoricalDataAsync(
            string sensorId,
            TimeSpan duration
        )
        {
            try
            {
                var db = await GetDatabaseAsync();
                var key = $"{_keyPrefix}{sensorId}";

                // Try to get data from the temporal buffer first
                var temporalData = await _temporalBuffer.GetSensorHistory(sensorId, duration);
                var temporalList = temporalData.ToList();

                if (temporalList.Any())
                {
                    return temporalList;
                }

                // If no temporal data, get the current value
                var value = await db.StringGetAsync(key);
                if (!value.HasValue)
                {
                    return Array.Empty<(DateTime, double)>();
                }

                if (!double.TryParse(value.ToString(), out var doubleValue))
                {
                    _logger.Warning("Invalid value for sensor {SensorId}: {Value}", sensorId, value);
                    return Array.Empty<(DateTime, double)>();
                }

                return new[] { (DateTime.UtcNow, doubleValue) };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting historical data for sensor {SensorId}", sensorId);
                return Array.Empty<(DateTime, double)>();
            }
        }

        public async Task SetSensorDataAsync(string sensorId, double value)
        {
            try
            {
                var db = await GetDatabaseAsync();
                var key = $"{_keyPrefix}{sensorId}";

                await db.StringSetAsync(key, value.ToString());
                await _temporalBuffer.AddSensorValue(sensorId, value);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error setting data for sensor {SensorId}", sensorId);
            }
        }

        public async Task SetSensorDataAsync(IDictionary<string, object> values)
        {
            if (values == null || !values.Any())
            {
                return;
            }

            try
            {
                var db = await GetDatabaseAsync();
                var batch = db.CreateBatch();
                var tasks = new List<Task>();

                foreach (var (key, value) in values)
                {
                    var redisKey = $"{_keyPrefix}{key}";
                    var redisValue = value?.ToString() ?? string.Empty;
                    tasks.Add(batch.StringSetAsync(redisKey, redisValue));

                    if (double.TryParse(redisValue, out var doubleValue))
                    {
                        tasks.Add(_temporalBuffer.AddSensorValue(key, doubleValue));
                    }
                }

                batch.Execute();
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error setting sensor data for {Count} sensors", values.Count);
            }
        }

        /// <summary>
        /// Returns all keys matching the <see cref="_keyPrefix"/>, using the same database index as <paramref name="db"/>.
        /// </summary>
        private async Task<RedisKey[]> GetAllSensorKeysAsync(IDatabase db)
        {
            var pattern = $"{_keyPrefix}*";
            var keys = new List<RedisKey>();

            var server = _connection.GetServer("localhost", 6379);
            if (server == null)
            {
                _logger.Warning("Unable to get Redis server for host=localhost:6379");
                return Array.Empty<RedisKey>();
            }

            await foreach (
                var key in server.KeysAsync(
                    db.Database,
                    pattern: pattern,
                    pageSize: 250,
                    flags: CommandFlags.None
                )
            )
            {
                keys.Add(key);
            }

            return keys.ToArray();
        }
    }
}
