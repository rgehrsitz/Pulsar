using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pulsar.Runtime.Storage;

/// <summary>
/// Interface for accessing the data store
/// </summary>
public interface IDataStore
{
    /// <summary>
    /// Gets a sensor value from the data store
    /// </summary>
    Task<double?> GetValueAsync(string sensorName);

    /// <summary>
    /// Sets a sensor value in the data store
    /// </summary>
    Task SetValueAsync(string sensorName, double value);

    /// <summary>
    /// Gets all sensor values from the data store
    /// </summary>
    Task<IDictionary<string, double>> GetAllValuesAsync();

    /// <summary>
    /// Gets the current values for all sensors (alias for GetAllValuesAsync)
    /// </summary>
    Task<IDictionary<string, double>> GetCurrentDataAsync() => GetAllValuesAsync();

    /// <summary>
    /// Checks if a sensor value has been above a threshold for a specified duration
    /// </summary>
    Task<bool> CheckThresholdOverTimeAsync(string sensorName, double threshold, TimeSpan duration);
}
