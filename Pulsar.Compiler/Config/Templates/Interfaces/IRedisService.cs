// File: Pulsar.Compiler/Config/Templates/Interfaces/IRedisService.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Beacon.Runtime.Interfaces
{
    /// <summary>
    /// Interface for Redis service operations
    /// </summary>
    public interface IRedisService
    {
        /// <summary>
        /// Gets all input values from Redis
        /// </summary>
        /// <returns>Dictionary of input values</returns>
        Task<Dictionary<string, object>> GetAllInputsAsync();

        /// <summary>
        /// Sets output values in Redis
        /// </summary>
        /// <param name="outputs">Dictionary of output values</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task SetOutputsAsync(Dictionary<string, object> outputs);

        /// <summary>
        /// Gets specific sensor values from Redis
        /// </summary>
        /// <param name="sensorKeys">List of sensor keys to retrieve</param>
        /// <returns>Dictionary of sensor values with timestamps</returns>
        Task<Dictionary<string, (double Value, DateTime Timestamp)>> GetSensorValuesAsync(
            IEnumerable<string> sensorKeys
        );

        /// <summary>
        /// Sets output values in Redis
        /// </summary>
        /// <param name="outputs">Dictionary of output values</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task SetOutputValuesAsync(Dictionary<string, double> outputs);

        /// <summary>
        /// Gets the values for a sensor over time
        /// </summary>
        /// <param name="sensor">The sensor key</param>
        /// <param name="count">Number of historical values to retrieve</param>
        /// <returns>Array of historical values</returns>
        Task<(double Value, DateTime Timestamp)[]> GetValues(string sensor, int count);

        /// <summary>
        /// Checks if Redis is healthy
        /// </summary>
        /// <returns>True if Redis is healthy, false otherwise</returns>
        bool IsHealthy { get; }

        /// <summary>
        /// Publishes a message to a Redis channel
        /// </summary>
        /// <param name="channel">The channel to publish to</param>
        /// <param name="message">The message to publish</param>
        /// <returns>The number of clients that received the message</returns>
        Task<long> PublishAsync(string channel, string message);
    }
}
