using System;
using System.Threading;
using System.Threading.Tasks;
using Beacon.PerformanceTester.Common;

namespace Beacon.PerformanceTester.OutputMonitor.Services
{
    /// <summary>
    /// Service for monitoring Beacon outputs and calculating performance metrics
    /// </summary>
    public interface IOutputMonitorService
    {
        /// <summary>
        /// Initialize the monitoring service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Monitor outputs for the specified test case
        /// </summary>
        /// <param name="testCase">Test case to monitor</param>
        /// <param name="startTime">Test start time</param>
        /// <param name="inputTimestamps">Dictionary of input timestamps for latency calculation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Test case result with performance metrics</returns>
        Task<TestCaseResult> MonitorOutputsAsync(
            TestCase testCase,
            DateTime startTime,
            Dictionary<string, List<(double Value, long Timestamp)>> inputTimestamps,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Close connections and clean up resources
        /// </summary>
        Task CloseAsync();
    }
}
