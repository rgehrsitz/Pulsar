using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Interface for accessing sensor data and historical values
/// </summary>
public interface IDataStore
{
    /// <summary>
    /// Gets the current values for all sensors
    /// </summary>
    Task<IDictionary<string, double>> GetCurrentDataAsync();

    /// <summary>
    /// Checks if a sensor value has been above a threshold for a specified duration
    /// </summary>
    Task<bool> CheckThresholdOverTimeAsync(string sensor, double threshold, TimeSpan duration);
}
