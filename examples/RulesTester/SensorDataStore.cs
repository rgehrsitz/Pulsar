using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Runtime.Engine;

namespace RulesTester;

public class SensorDataStore : IDataStore
{
    private readonly ISensorDataProvider _sensorDataProvider;

    public SensorDataStore(ISensorDataProvider sensorDataProvider)
    {
        _sensorDataProvider = sensorDataProvider;
    }

    public Task<IDictionary<string, double>> GetCurrentDataAsync()
    {
        return _sensorDataProvider.GetCurrentDataAsync();
    }

    public async Task<bool> CheckThresholdOverTimeAsync(string sensor, double threshold, TimeSpan duration)
    {
        var history = await _sensorDataProvider.GetHistoricalDataAsync(sensor, duration);
        
        foreach (var (_, value) in history)
        {
            if (value <= threshold)
            {
                return false;
            }
        }

        return history.Count > 0;
    }
}
