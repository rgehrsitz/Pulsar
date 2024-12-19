using NRedisStack;
using NRedisStack.RedisStackCommands;
using StackExchange.Redis;
using Serilog;
using Pulsar.Runtime.Engine;
using NRedisStack.DataTypes;

namespace Pulsar.Runtime.Storage;

/// <summary>
/// Provides sensor data access using Redis as the backend store
/// </summary>
public class RedisSensorDataProvider : ISensorDataProvider
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger _logger;
    private readonly string _keyPrefix;
    private readonly string _tsPrefix;

    public RedisSensorDataProvider(IConnectionMultiplexer redis, ILogger logger, string keyPrefix = "sensor:", string tsPrefix = "ts:")
    {
        _redis = redis;
        _logger = logger.ForContext<RedisSensorDataProvider>();
        _keyPrefix = keyPrefix;
        _tsPrefix = tsPrefix;
    }

    public async Task<IDictionary<string, double>> GetCurrentDataAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            var keys = await GetAllSensorKeys(db);
            
            if (!keys.Any())
            {
                return new Dictionary<string, double>();
            }

            // Use MGET to get all values in a single operation
            var values = await db.StringGetAsync(keys.ToArray());
            var result = new Dictionary<string, double>();

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
                    // Remove prefix from key when returning
                    var sensorKey = key.ToString().Substring(_keyPrefix.Length);
                    result[sensorKey] = doubleValue;
                }
                else
                {
                    _logger.Warning("Invalid value format for key {Key}: {Value}", key, value);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting sensor data from Redis");
            throw;
        }
    }

    public async Task<IReadOnlyList<(DateTime Timestamp, double Value)>> GetHistoricalDataAsync(string sensorName, TimeSpan duration)
    {
        try
        {
            var db = _redis.GetDatabase();
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

        try
        {
            var db = _redis.GetDatabase();
            var ts = db.TS();
            var batch = db.CreateBatch();
            var tasks = new List<Task>();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            foreach (var (key, value) in values)
            {
                // Set current value
                var redisKey = new RedisKey($"{_keyPrefix}{key}");
                var redisValue = new RedisValue(value?.ToString() ?? string.Empty);
                tasks.Add(batch.StringSetAsync(redisKey, redisValue));

                // Store in time series if value is numeric
                if (double.TryParse(value?.ToString(), out var doubleValue))
                {
                    var tsKey = $"{_tsPrefix}{key}";
                    // Ensure time series exists (creates if not exists)
                    await ts.CreateAsync(tsKey);
                    tasks.Add(ts.AddAsync(tsKey, now, doubleValue));
                }
            }

            batch.Execute();
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error setting sensor data in Redis");
            throw;
        }
    }

    private async Task<RedisKey[]> GetAllSensorKeys(IDatabase db)
    {
        try
        {
            // Use SCAN to get all keys with our prefix
            var keys = new List<RedisKey>();
            var pattern = $"{_keyPrefix}*";
            var enumerator = db.Multiplexer.GetServer(db.Multiplexer.GetEndPoints().First())
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
