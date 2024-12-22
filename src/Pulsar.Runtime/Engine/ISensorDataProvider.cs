namespace Pulsar.Runtime.Engine;

/// <summary>
/// Interface for providing sensor data to the rule engine
/// </summary>
public interface ISensorDataProvider
{
    /// <summary>
    /// Gets the current sensor data
    /// </summary>
    /// <returns>Dictionary of sensor names to their current values</returns>
    Task<IDictionary<string, double>> GetCurrentDataAsync();

    /// <summary>
    /// Gets historical sensor data for a specific time window
    /// </summary>
    /// <param name="sensorName">Name of the sensor</param>
    /// <param name="duration">Time window duration</param>
    /// <returns>List of sensor values within the specified time window</returns>
    Task<IReadOnlyList<(DateTime Timestamp, double Value)>> GetHistoricalDataAsync(
        string sensorName,
        TimeSpan duration
    );

    /// <summary>
    /// Sets multiple sensor values
    /// </summary>
    /// <param name="values">Dictionary of sensor names to their values</param>
    Task SetSensorDataAsync(IDictionary<string, object> values);
}
