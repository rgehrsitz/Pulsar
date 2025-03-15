using System;
using System.Threading;
using System.Threading.Tasks;

namespace Beacon.PerformanceTester.OutputMonitor.Services
{
    /// <summary>
    /// Service for monitoring Beacon process metrics (CPU, memory)
    /// </summary>
    public interface IProcessMonitorService
    {
        /// <summary>
        /// Initialize the process monitor
        /// </summary>
        /// <param name="processName">Name of the process to monitor (e.g., "Beacon")</param>
        Task InitializeAsync(string processName);

        /// <summary>
        /// Start monitoring process metrics
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task StartMonitoringAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Stop monitoring and return metrics
        /// </summary>
        /// <returns>Tuple with average CPU usage and peak memory usage</returns>
        Task<(double AvgCpuPercent, double PeakMemoryMB)> StopMonitoringAsync();

        /// <summary>
        /// Close and clean up resources
        /// </summary>
        Task CloseAsync();
    }
}
