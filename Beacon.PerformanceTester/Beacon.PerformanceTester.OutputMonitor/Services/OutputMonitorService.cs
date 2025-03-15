using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Beacon.PerformanceTester.Common;
using Microsoft.Extensions.Logging;

namespace Beacon.PerformanceTester.OutputMonitor.Services
{
    /// <summary>
    /// Service for monitoring test outputs and calculating performance metrics
    /// </summary>
    public class OutputMonitorService : IOutputMonitorService
    {
        private readonly ILogger<OutputMonitorService> _logger;
        private readonly IRedisMonitorService _redisMonitor;
        private readonly IProcessMonitorService _processMonitor;

        // Store output results
        private readonly ConcurrentDictionary<string, OutputResult> _outputResults = new();

        // Track the completion status of each expected output
        private readonly ConcurrentDictionary<string, bool> _outputCompletion = new();

        // Wait handle for test completion
        private TaskCompletionSource<bool>? _testCompletionSource;

        // Start time of the current test
        private DateTime _testStartTime;

        public OutputMonitorService(
            ILogger<OutputMonitorService> logger,
            IRedisMonitorService redisMonitor,
            IProcessMonitorService processMonitor
        )
        {
            _logger = logger;
            _redisMonitor = redisMonitor;
            _processMonitor = processMonitor;
        }

        /// <summary>
        /// Initialize the monitor service
        /// </summary>
        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing output monitor service");
            await _redisMonitor.InitializeAsync();
            await _processMonitor.InitializeAsync("Beacon");
        }

        /// <summary>
        /// Monitor outputs for a test case
        /// </summary>
        public async Task<TestCaseResult> MonitorOutputsAsync(
            TestCase testCase,
            DateTime startTime,
            Dictionary<string, List<(double Value, long Timestamp)>> inputTimestamps,
            CancellationToken cancellationToken
        )
        {
            _logger.LogInformation(
                "Starting output monitoring for test case: {TestCase}",
                testCase.Name
            );

            // Reset state
            _testStartTime = startTime;
            _outputResults.Clear();
            _outputCompletion.Clear();
            _testCompletionSource = new TaskCompletionSource<bool>();

            // Initialize completion tracking
            foreach (var output in testCase.ExpectedOutputs)
            {
                _outputCompletion[output.Key] = false;
            }

            try
            {
                // Start process monitoring
                await _processMonitor.StartMonitoringAsync(cancellationToken);

                // Setup Redis monitoring for each expected output
                foreach (var expectedOutput in testCase.ExpectedOutputs)
                {
                    await _redisMonitor.MonitorKeyAsync(
                        expectedOutput.Key,
                        async (key, value, timestamp) =>
                        {
                            await ProcessOutputValueAsync(
                                expectedOutput,
                                key,
                                value,
                                timestamp,
                                inputTimestamps
                            );
                        },
                        cancellationToken
                    );
                }

                // Create a timeout task for the test duration plus a small buffer
                var timeoutTask = Task.Delay(
                    TimeSpan.FromSeconds(testCase.DurationSeconds + 5),
                    cancellationToken
                );

                // Wait for either all outputs to be received or timeout
                await Task.WhenAny(_testCompletionSource.Task, timeoutTask);

                // Get process metrics
                var (avgCpu, peakMemory) = await _processMonitor.StopMonitoringAsync();

                // Calculate final results
                return CompileTestResults(testCase, avgCpu, peakMemory);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error monitoring outputs for test case {TestCase}",
                    testCase.Name
                );

                // Create a failure result
                return new TestCaseResult
                {
                    TestCaseName = testCase.Name,
                    Success = false,
                    DurationMs = (DateTime.UtcNow - _testStartTime).TotalMilliseconds,
                };
            }
        }

        /// <summary>
        /// Process a new output value from Redis
        /// </summary>
        private async Task ProcessOutputValueAsync(
            ExpectedOutput expectedOutput,
            string key,
            string value,
            long timestamp,
            Dictionary<string, List<(double Value, long Timestamp)>> inputTimestamps
        )
        {
            try
            {
                // Calculate time since test start
                var detectionTime = DateTime.UtcNow;
                var timeTakenMs = (detectionTime - _testStartTime).TotalMilliseconds;

                // Parse the value based on expected type
                object? parsedValue = value;
                if (expectedOutput.Value != null)
                {
                    if (
                        expectedOutput.Value is double
                        || expectedOutput.Value is int
                        || expectedOutput.Value is float
                    )
                    {
                        if (double.TryParse(value, out double numericValue))
                        {
                            parsedValue = numericValue;
                        }
                    }
                    else if (expectedOutput.Value is bool)
                    {
                        if (bool.TryParse(value, out bool boolValue))
                        {
                            parsedValue = boolValue;
                        }
                    }
                }

                // Check if value matches expected value with tolerance
                bool isMatch = false;
                if (expectedOutput.Value != null && parsedValue != null)
                {
                    if (
                        parsedValue is double numeric
                        && expectedOutput.Value is double expectedNumeric
                    )
                    {
                        isMatch = Math.Abs(numeric - expectedNumeric) <= expectedOutput.Tolerance;
                    }
                    else
                    {
                        isMatch = parsedValue.ToString() == expectedOutput.Value.ToString();
                    }
                }

                // Calculate end-to-end latency from closest input
                double latencyMs = 0;
                if (inputTimestamps.Count > 0)
                {
                    // Find the most recent input timestamp before this output
                    long outputTimestamp = timestamp;
                    long closestInputTimestamp = 0;

                    // Look for the closest input timestamp
                    foreach (var inputEntry in inputTimestamps)
                    {
                        foreach (var entry in inputEntry.Value.OrderByDescending(e => e.Timestamp))
                        {
                            if (
                                entry.Timestamp <= outputTimestamp
                                && (
                                    closestInputTimestamp == 0
                                    || entry.Timestamp > closestInputTimestamp
                                )
                            )
                            {
                                closestInputTimestamp = entry.Timestamp;
                            }
                        }
                    }

                    if (closestInputTimestamp > 0)
                    {
                        latencyMs = outputTimestamp - closestInputTimestamp;
                    }
                }

                // Create or update the output result
                var outputResult = new OutputResult
                {
                    Key = key,
                    ActualValue = parsedValue,
                    ExpectedValue = expectedOutput.Value,
                    IsMatch = isMatch,
                    TimeTakenMs = timeTakenMs,
                    LatencyMs = latencyMs,
                };

                _outputResults[key] = outputResult;

                _logger.LogDebug(
                    "Output detected: {Key}={Value}, Match={IsMatch}, Latency={Latency}ms",
                    key,
                    parsedValue,
                    isMatch,
                    latencyMs
                );

                // Mark this output as complete
                _outputCompletion[key] = true;

                // Check if all expected outputs have been received
                if (_outputCompletion.Values.All(v => v))
                {
                    _logger.LogInformation("All expected outputs received for test case");
                    _testCompletionSource?.TrySetResult(true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing output value for {Key}", key);
            }
        }

        /// <summary>
        /// Compile final test results
        /// </summary>
        private TestCaseResult CompileTestResults(
            TestCase testCase,
            double avgCpu,
            double peakMemory
        )
        {
            var result = new TestCaseResult
            {
                TestCaseName = testCase.Name,
                OutputResults = new List<OutputResult>(),
                DurationMs = (DateTime.UtcNow - _testStartTime).TotalMilliseconds,
                AvgCpuPercent = avgCpu,
                PeakMemoryMB = peakMemory,
            };

            // Add all output results
            foreach (var expectedOutput in testCase.ExpectedOutputs)
            {
                if (_outputResults.TryGetValue(expectedOutput.Key, out var outputResult))
                {
                    result.OutputResults.Add(outputResult);
                }
                else
                {
                    // Add a placeholder for missing output
                    result.OutputResults.Add(
                        new OutputResult
                        {
                            Key = expectedOutput.Key,
                            ExpectedValue = expectedOutput.Value,
                            ActualValue = null,
                            IsMatch = false,
                            TimeTakenMs = result.DurationMs,
                            LatencyMs = 0,
                        }
                    );
                }
            }

            // Calculate success and latency metrics
            result.Success = result.OutputResults.All(r => r.IsMatch);

            if (result.OutputResults.Count > 0)
            {
                var latencies = result
                    .OutputResults.Where(r => r.LatencyMs > 0)
                    .Select(r => r.LatencyMs)
                    .ToList();
                if (latencies.Count > 0)
                {
                    result.AverageLatencyMs = latencies.Average();
                    result.MaxLatencyMs = latencies.Max();

                    // Calculate 95th percentile latency
                    latencies.Sort();
                    int index95 = (int)Math.Ceiling(latencies.Count * 0.95) - 1;
                    if (index95 >= 0 && index95 < latencies.Count)
                    {
                        result.P95LatencyMs = latencies[index95];
                    }
                    else
                    {
                        result.P95LatencyMs = result.MaxLatencyMs;
                    }
                }
            }

            _logger.LogInformation(
                "Test case {TestCase} completed. Success={Success}, Avg Latency={AvgLatency}ms, P95={P95}ms, Max={MaxLatency}ms",
                testCase.Name,
                result.Success,
                result.AverageLatencyMs,
                result.P95LatencyMs,
                result.MaxLatencyMs
            );

            return result;
        }

        /// <summary>
        /// Close and clean up resources
        /// </summary>
        public async Task CloseAsync()
        {
            _logger.LogInformation("Closing output monitor service");
            await _redisMonitor.CloseAsync();
            await _processMonitor.CloseAsync();
        }
    }
}
