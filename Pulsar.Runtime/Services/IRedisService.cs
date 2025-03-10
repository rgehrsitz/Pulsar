// File: Pulsar.Runtime/Services/IRedisService.cs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Beacon.Runtime.Services
{
    public interface IRedisService
    {
        /// <summary>
        /// Gets multiple sensor values by name
        /// </summary>
        Task<Dictionary<string, double>> GetSensorValuesAsync(IEnumerable<string> sensorNames);
        
        /// <summary>
        /// Sets multiple output values
        /// </summary>
        Task SetOutputValuesAsync(Dictionary<string, double> outputs);
        
        /// <summary>
        /// Gets all input values
        /// </summary>
        Task<Dictionary<string, object>> GetAllInputsAsync();
        
        /// <summary>
        /// Sets multiple output values
        /// </summary>
        Task SetOutputsAsync(Dictionary<string, object> outputs);
        
        /// <summary>
        /// Checks if Redis connection is healthy
        /// </summary>
        Task<bool> IsHealthyAsync();
    }
}