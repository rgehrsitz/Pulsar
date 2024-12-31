using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Runtime.Services;
using Serilog;
using StackExchange.Redis;

namespace Pulsar.Runtime.Storage;

/// <summary>
/// Redis implementation of IDataStore
/// </summary>
public class RedisDataStore : IDataStore
{
    private readonly ConnectionMultiplexer _connection;
    private readonly ILogger _logger;
    private readonly string _keyPrefix;
    private readonly TimeSeriesService _timeSeriesService;
    private IDatabase? _db;

    public RedisDataStore(
        ConnectionMultiplexer connection,
        ILogger logger,
        TimeSeriesService timeSeriesService,
        string keyPrefix = "sensor:"
    )
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger.ForContext<RedisDataStore>();
        _keyPrefix = keyPrefix;
        _timeSeriesService = timeSeriesService;
        _db = _connection.GetDatabase();
    }

    public async Task<double?> GetValueAsync(string sensorName)
    {
        try
        {
            var value = await _db!.StringGetAsync(_keyPrefix + sensorName);
            return value.HasValue ? (double?)double.Parse(value!) : null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting sensor value for {SensorName}", sensorName);
            return null;
        }
    }

    public async Task SetValueAsync(string sensorName, double value)
    {
        try
        {
            await _db!.StringSetAsync(_keyPrefix + sensorName, value.ToString());
            _timeSeriesService.Update(sensorName, value);

            // Additional logging for debugging
            _logger.Information("Set value for {SensorName} to {Value}", sensorName, value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error setting sensor value for {SensorName}", sensorName);
            throw;
        }
    }

    public async Task<IDictionary<string, double>> GetAllValuesAsync()
    {
        var result = new Dictionary<string, double>();
        try
        {
            var server = _connection.GetServer(_connection.GetEndPoints()[0]);
            var keys = server.Keys(pattern: _keyPrefix + "*");

            foreach (var key in keys)
            {
                var value = await _db!.StringGetAsync(key);
                if (value.HasValue && double.TryParse(value!, out var doubleValue))
                {
                    var sensorName = key.ToString()[_keyPrefix.Length..];
                    result[sensorName] = doubleValue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting all sensor values");
            throw;
        }
        return result;
    }

    public Task<bool> CheckThresholdOverTimeAsync(
        string sensorName,
        double threshold,
        TimeSpan duration
    )
    {
        return _timeSeriesService.CheckThresholdOverTimeAsync(sensorName, threshold, duration);
    }
}
