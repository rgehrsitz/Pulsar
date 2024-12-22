// Filename: RedisSensorDataProvider.cs
using NRedisStack;
using NRedisStack.RedisStackCommands;
using NRedisStack.DataTypes;
using NRedisStack.Literals.Enums;
using StackExchange.Redis;
using Serilog;
using Pulsar.Runtime.Engine;
using System.Threading;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

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
        private readonly string _tsPrefix;
        private readonly SemaphoreSlim _reconnectLock = new SemaphoreSlim(1, 1);

        private IDatabase? _db; // We lazily acquire and test the DB connection
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 1000;

        public RedisSensorDataProvider(
            IConnectionMultiplexer connection,
            ILogger logger,
            string keyPrefix = "sensor:",
            string tsPrefix = "ts:")
        {
            _connection = connection;
            _logger = logger.ForContext<RedisSensorDataProvider>();
            _keyPrefix = keyPrefix;
            _tsPrefix = tsPrefix;
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
        private async Task<T> ExecuteWithRetryAsync<T>(Func<IDatabase, Task<T>> operation, string operationName)
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
                    _logger.Warning(ex,
                        "Redis connection error during {Operation} (attempt {Attempt}/{MaxRetries})",
                        operationName, attempt, MaxRetries);

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

            throw new InvalidOperationException($"Failed to execute {operationName} after {MaxRetries} attempts");
        }

        public async Task<IDictionary<string, double>> GetCurrentDataAsync()
        {
            return await ExecuteWithRetryAsync(async db =>
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
                        _logger.Warning("Invalid value format for key {Key}: {Value}", key, redisValue);
                    }
                }

                return result;
            }, "GetCurrentData");
        }

        public async Task<IReadOnlyList<(DateTime Timestamp, double Value)>> GetHistoricalDataAsync(string sensorName, TimeSpan duration)
        {
            return await ExecuteWithRetryAsync(async db =>
            {
                // 1) Use db.TS() instead of db.TSAsync()
                var ts = db.TS();
                var tsKey = $"{_tsPrefix}{sensorName}";

                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var fromMs = nowMs - (long)duration.TotalMilliseconds;

                var range = await ts.RangeAsync(tsKey, fromMs, nowMs);
                return range.Select(entry => (
                    Timestamp: DateTimeOffset.FromUnixTimeMilliseconds(entry.Time).UtcDateTime,
                    Value: entry.Val
                )).ToList();
            }, "GetHistoricalData");
        }

        public async Task SetSensorDataAsync(IDictionary<string, object> values)
        {
            if (!values.Any())
            {
                // Nothing to do
                return;
            }

            await ExecuteWithRetryAsync(async db =>
            {
                // 1) Use db.TS() instead of db.TSAsync()
                var ts = db.TS();
                var batch = db.CreateBatch();
                var tasks = new List<Task>();

                // We'll use the same timestamp for everything for simplicity
                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                foreach (var (key, value) in values)
                {
                    var redisKey = (RedisKey) ($"{_keyPrefix}{key}");
                    var redisValue = value?.ToString() ?? string.Empty;

                    // Queue up a StringSet in the batch
                    tasks.Add(batch.StringSetAsync(redisKey, redisValue));

                    // If it's numeric, record in time series as well
                    if (double.TryParse(redisValue, out var doubleValue))
                    {
                        var tsKey = $"{_tsPrefix}{key}";

                        // Initialize TsCreateParams using TsCreateParamsBuilder
                        var createParamsBuilder = new TsCreateParamsBuilder()
                            .RetentionTime(TimeSpan.FromHours(1))
                            .Uncompressed()
                            .ChunkSize(4096)
                            .DuplicatePolicy(TsDuplicatePolicy.LAST)
                            .WithLabels(new List<TimeSeriesLabel> {
                                new("sensor", key),
                                new("unit", "generic")
                            });
                        var createParams = createParamsBuilder.Build();

                        // Safely create the series (no error if exists)
                        await ts.CreateAsync(tsKey, createParams);

                        // Initialize TsAddParams using TsAddParamsBuilder
                        var addParamsBuilder = new TsAddParamsBuilder()
                            .DuplicatePolicy(TsDuplicatePolicy.SUM);
                        var addParams = addParamsBuilder.Build();

                        // Add the measurement with timestamp
                        await ts.AddAsync(tsKey, new TimeStamp(nowMs), doubleValue, addParams);
                    }
                }

                // Execute the batch sets
                batch.Execute();
                await Task.WhenAll(tasks);

                return true;
            }, "SetSensorData");
        }

        /// <summary>
        /// Returns all keys matching the <see cref="_keyPrefix"/>, using the same database index as <paramref name="db"/>.
        /// </summary>
        private async Task<RedisKey[]> GetAllSensorKeysAsync(IDatabase db)
        {
            var pattern = $"{_keyPrefix}*";
            var keys = new List<RedisKey>();

            // 2) Use .GetServer(...) instead of .GetServerAsync(...)
            var server = _connection.GetServer("localhost", 6379);
            if (server == null)
            {
                _logger.Warning("Unable to get Redis server for host=localhost:6379");
                return Array.Empty<RedisKey>();
            }

            // The .KeysAsync(...) method returns IAsyncEnumerable<RedisKey> we can iterate over with 'await foreach'
            await foreach (var key in server.KeysAsync(
                db.Database,
                pattern: pattern,
                pageSize: 250,
                flags: CommandFlags.None))
            {
                keys.Add(key);
            }

            return keys.ToArray();
        }
    }
}
