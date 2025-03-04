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
        /// Gets sensor values from Redis
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
        /// Checks if Redis is healthy
        /// </summary>
        /// <returns>True if Redis is healthy, false otherwise</returns>
        bool IsHealthy { get; }
    }
}
