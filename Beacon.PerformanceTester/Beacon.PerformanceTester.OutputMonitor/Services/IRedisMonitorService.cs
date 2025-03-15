using System;
using System.Threading;
using System.Threading.Tasks;

namespace Beacon.PerformanceTester.OutputMonitor.Services
{
    /// <summary>
    /// Service for monitoring Redis key changes
    /// </summary>
    public interface IRedisMonitorService
    {
        /// <summary>
        /// Initialize the Redis monitor
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Start monitoring a specific Redis key for changes
        /// </summary>
        /// <param name="key">Redis key to monitor</param>
        /// <param name="callback">Callback function to call when key changes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task MonitorKeyAsync(
            string key,
            Func<string, string, long, Task> callback,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Get the current timestamp and value for a key
        /// </summary>
        /// <param name="key">Redis key to check</param>
        /// <returns>Tuple with value and timestamp, or null if not found</returns>
        Task<(string Value, long Timestamp)?> GetValueWithTimestampAsync(string key);

        /// <summary>
        /// Close connections and clean up resources
        /// </summary>
        Task CloseAsync();
    }
}
