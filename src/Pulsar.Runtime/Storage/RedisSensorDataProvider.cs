using NRedisStack;
using NRedisStack.RedisStackCommands;
using StackExchange.Redis;
using Serilog;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Configuration;
using NRedisStack.DataTypes;
using System.Threading;

namespace Pulsar.Runtime.Storage;

/// <summary>
/// Provides sensor data access using Redis as the backend store
/// </summary>
public class RedisSensorDataProvider : ISensorDataProvider
{
    private readonly RedisClusterConfiguration _clusterConfig;
    private readonly ILogger _logger;
    private readonly string _keyPrefix;
    private readonly string _tsPrefix;
    private readonly SemaphoreSlim _reconnectLock = new SemaphoreSlim(1, 1);
    private IDatabase _db;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 1000;

    public RedisSensorDataProvider(
        RedisClusterConfiguration clusterConfig,
        ILogger logger, 
        string keyPrefix = "sensor:", 
        string tsPrefix = "ts:")
    {
        _clusterConfig = clusterConfig;
        _logger = logger.ForContext<RedisSensorDataProvider>();
        _keyPrefix = keyPrefix;
        _tsPrefix = tsPrefix;
    }

    private async Task<IDatabase> GetDatabaseAsync()
    {
        if (_db != null)
        {
            try
            {
                // Test the connection with a simple ping
                await _db.PingAsync();
                return _db;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Redis connection test failed, attempting to reconnect");
                _db = null;
            }
        }

        await _reconnectLock.WaitAsync();
        try
        {
            if (_db == null)
            {
                var connection = _clusterConfig.GetConnection();
                _db = connection.GetDatabase();
                _logger.Information("Successfully connected to Redis");
            }
            return _db;
        }
        finally
        {
            _reconnectLock.Release();
        }
    }

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
                _logger.Warning(ex, "Redis connection error during {Operation} (attempt {Attempt}/{MaxRetries})",
                    operationName, attempt, MaxRetries);
                
                if (attempt == MaxRetries)
                    throw;

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
            var keys = await GetAllSensorKeys(db);

            if (!keys.Any())
            {
                return result;
            }

            var values = await db.StringGetAsync(keys.ToArray());

            for (var i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                var value = values[i];

                if (!value.HasValue)
                {
                    continue;
                }

                if (double.TryParse(value.ToString(), out var doubleValue))
                {
                    var sensorKey = key.ToString().Substring(_keyPrefix.Length);
                    result[sensorKey] = doubleValue;
                }
                else
                {
                    _logger.Warning("Invalid value format for key {Key}: {Value}", key, value);
                }
            }

            return result;
        }, "GetCurrentData");
    }

    public async Task<IReadOnlyList<(DateTime Timestamp, double Value)>> GetHistoricalDataAsync(string sensorName, TimeSpan duration)
    {
        try
        {
            var db = await GetDatabaseAsync();
            var ts = db.TS();
            var tsKey = $"{_tsPrefix}{sensorName}";

            // Calculate the time range
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var from = now - (long)duration.TotalMilliseconds;

            // Query the time series
            var range = await ts.RangeAsync(tsKey, from, now);
            return range.Select(p => (
                Timestamp: DateTimeOffset.FromUnixTimeMilliseconds(p.Time).UtcDateTime,
                Value: p.Val
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting historical data for sensor {SensorName}", sensorName);
            throw;
        }
    }

    public async Task SetSensorDataAsync(IDictionary<string, object> values)
    {
        if (!values.Any())
        {
            return;
        }

        await ExecuteWithRetryAsync(async db =>
        {
            var ts = db.TS();
            var batch = db.CreateBatch();
            var tasks = new List<Task>();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            foreach (var (key, value) in values)
            {
                var redisKey = new RedisKey($"{_keyPrefix}{key}");
                var redisValue = new RedisValue(value?.ToString() ?? string.Empty);
                tasks.Add(batch.StringSetAsync(redisKey, redisValue));

                if (double.TryParse(value?.ToString(), out var doubleValue))
                {
                    var tsKey = $"{_tsPrefix}{key}";
                    await ts.CreateAsync(tsKey);
                    tasks.Add(ts.AddAsync(tsKey, now, doubleValue));
                }
            }

            batch.Execute();
            await Task.WhenAll(tasks);
            return true;
        }, "SetSensorData");
    }

    private async Task<RedisKey[]> GetAllSensorKeys(IDatabase db)
    {
        try
        {
            var keys = new List<RedisKey>();
            var pattern = $"{_keyPrefix}*";
            var enumerator = _clusterConfig.GetConnection()
                .GetServer(_clusterConfig.GetCurrentMaster())
                .KeysAsync(pattern: pattern);

            await foreach (var key in enumerator)
            {
                keys.Add(key);
            }

            return keys.ToArray();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting sensor keys from Redis");
            throw;
        }
    }
}
