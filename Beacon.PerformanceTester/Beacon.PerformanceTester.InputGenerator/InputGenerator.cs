using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Beacon.PerformanceTester.Common;
using Beacon.PerformanceTester.InputGenerator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Beacon.PerformanceTester.InputGenerator
{
    /// <summary>
    /// Main class for generating test inputs to Redis
    /// </summary>
    public class InputGenerator
    {
        private readonly ILogger<InputGenerator> _logger;
        private readonly IRedisDataService _redisService;
        private readonly IPatternGeneratorFactory _patternGeneratorFactory;
        private readonly IConfiguration _configuration;

        // Track input timestamps for latency calculation
        private ConcurrentDictionary<
            string,
            List<(double Value, long Timestamp)>
        > _inputTimestamps = new();

        public InputGenerator(
            ILogger<InputGenerator> logger,
            IRedisDataService redisService,
            IPatternGeneratorFactory patternGeneratorFactory,
            IConfiguration configuration
        )
        {
            _logger = logger;
            _redisService = redisService;
            _patternGeneratorFactory = patternGeneratorFactory;
            _configuration = configuration;
        }

        /// <summary>
        /// Run a complete test scenario
        /// </summary>
        public async Task<TestScenarioResult> RunScenarioAsync(
            TestScenario scenario,
            CancellationToken cancellationToken
        )
        {
            _logger.LogInformation("Starting test scenario: {ScenarioName}", scenario.Name);

            var scenarioResult = new TestScenarioResult
            {
                ScenarioName = scenario.Name,
                StartTime = DateTime.UtcNow,
                TestCaseResults = new List<TestCaseResult>(),
            };

            try
            {
                // Initialize Redis connection
                await _redisService.InitializeAsync();

                // Clear any existing data
                await _redisService.ClearTestDataAsync();

                bool allPassed = true;

                // Run each test case
                foreach (var testCase in scenario.TestCases)
                {
                    var result = await RunTestCaseAsync(testCase, cancellationToken);
                    scenarioResult.TestCaseResults.Add(result);

                    allPassed = allPassed && result.Success;

                    if (!result.Success && scenario.AbortOnFailure)
                    {
                        _logger.LogWarning("Aborting scenario due to test failure");
                        break;
                    }

                    // Delay between test cases
                    if (
                        scenario.DelayBetweenTestsSeconds > 0
                        && testCase != scenario.TestCases.Last()
                    )
                    {
                        _logger.LogInformation(
                            "Waiting {Seconds} seconds before next test...",
                            scenario.DelayBetweenTestsSeconds
                        );

                        await Task.Delay(
                            TimeSpan.FromSeconds(scenario.DelayBetweenTestsSeconds),
                            cancellationToken
                        );

                        // Clear Redis data between tests
                        await _redisService.ClearTestDataAsync();
                    }
                }

                // Complete the scenario result
                scenarioResult.EndTime = DateTime.UtcNow;
                scenarioResult.TotalDurationMs = (
                    scenarioResult.EndTime - scenarioResult.StartTime
                ).TotalMilliseconds;
                scenarioResult.Success = allPassed;

                _logger.LogInformation(
                    "Scenario completed. Result: {Result}",
                    allPassed ? "SUCCESS" : "FAILURE"
                );

                return scenarioResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running test scenario");

                // Complete the scenario result with failure
                scenarioResult.EndTime = DateTime.UtcNow;
                scenarioResult.TotalDurationMs = (
                    scenarioResult.EndTime - scenarioResult.StartTime
                ).TotalMilliseconds;
                scenarioResult.Success = false;

                return scenarioResult;
            }
            finally
            {
                // Clean up
                await _redisService.CloseAsync();
            }
        }

        /// <summary>
        /// Run a single test case
        /// </summary>
        private async Task<TestCaseResult> RunTestCaseAsync(
            TestCase testCase,
            CancellationToken cancellationToken
        )
        {
            _logger.LogInformation(
                "Running test case: {TestCaseName} ({Duration}s)",
                testCase.Name,
                testCase.DurationSeconds
            );

            // Reset input timestamps
            _inputTimestamps.Clear();

            try
            {
                // Create pattern generators for each input
                var inputGenerators = testCase
                    .Inputs.Select(input => new
                    {
                        Input = input,
                        Generator = _patternGeneratorFactory.CreateGenerator(input),
                    })
                    .ToList();

                if (inputGenerators.Count == 0)
                {
                    _logger.LogWarning("No inputs configured for test case");
                    return new TestCaseResult
                    {
                        TestCaseName = testCase.Name,
                        Success = false,
                        OutputResults = new List<OutputResult>(),
                        DurationMs = 0,
                    };
                }

                // Setup tasks to write to Redis at specified update frequencies
                var tasks = new List<Task>();
                var startTime = DateTime.UtcNow;
                var stopwatch = Stopwatch.StartNew();

                // Initialize the input timestamp tracking
                foreach (var input in inputGenerators)
                {
                    _inputTimestamps[input.Input.Key] = new List<(double Value, long Timestamp)>();
                }

                foreach (var input in inputGenerators)
                {
                    // Create a separate task for each input's update frequency
                    tasks.Add(
                        Task.Run(
                            async () =>
                            {
                                try
                                {
                                    // Calculate number of iterations based on frequency and duration
                                    int updateFrequencyMs = input.Input.UpdateFrequencyMs;
                                    int iterations =
                                        (testCase.DurationSeconds * 1000) / updateFrequencyMs;

                                    for (
                                        int i = 0;
                                        i < iterations
                                            && !cancellationToken.IsCancellationRequested;
                                        i++
                                    )
                                    {
                                        // Wait for appropriate update interval
                                        if (i > 0)
                                        {
                                            await Task.Delay(updateFrequencyMs, cancellationToken);
                                        }

                                        // Generate value based on elapsed time
                                        long elapsedMs = stopwatch.ElapsedMilliseconds;
                                        var value = input.Generator.GenerateValue(elapsedMs);

                                        // Write to Redis with current timestamp
                                        await _redisService.SetValueAsync(
                                            input.Input.Key,
                                            value,
                                            elapsedMs
                                        );

                                        // Record the timestamp for this input (for latency calculation)
                                        if (
                                            _inputTimestamps.TryGetValue(
                                                input.Input.Key,
                                                out var timestamps
                                            )
                                        )
                                        {
                                            timestamps.Add((value, elapsedMs));
                                        }

                                        if (i % 10 == 0 || i == iterations - 1)
                                        {
                                            _logger.LogDebug(
                                                "Updated {Key} = {Value} ({Iteration}/{Total})",
                                                input.Input.Key,
                                                value,
                                                i + 1,
                                                iterations
                                            );
                                        }
                                    }

                                    _logger.LogInformation(
                                        "Completed updates for {Key}",
                                        input.Input.Key
                                    );
                                }
                                catch (Exception ex)
                                    when (ex is TaskCanceledException
                                        || ex is OperationCanceledException
                                    )
                                {
                                    _logger.LogInformation(
                                        "Updates for {Key} were canceled",
                                        input.Input.Key
                                    );
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(
                                        ex,
                                        "Error generating input for {Key}",
                                        input.Input.Key
                                    );
                                }
                            },
                            cancellationToken
                        )
                    );
                }

                // Create a timeout task
                var timeoutTask = Task.Run(
                    async () =>
                    {
                        try
                        {
                            // Wait for the specified test duration plus a small buffer
                            await Task.Delay(
                                TimeSpan.FromSeconds(testCase.DurationSeconds + 2),
                                cancellationToken
                            );
                            _logger.LogInformation(
                                "Test case duration ({Duration}s) completed",
                                testCase.DurationSeconds
                            );
                        }
                        catch (TaskCanceledException)
                        {
                            _logger.LogInformation("Test was canceled before completion");
                        }
                    },
                    cancellationToken
                );

                tasks.Add(timeoutTask);

                // Wait for all tasks to complete
                await Task.WhenAll(tasks);

                // Connect to output monitor to get results
                var result = await ConnectToOutputMonitorAsync(
                    testCase,
                    startTime,
                    _inputTimestamps
                );

                _logger.LogInformation(
                    "Test case {TestCase} completed. Success: {Success}",
                    testCase.Name,
                    result.Success
                );

                return result;
            }
            catch (Exception ex)
                when (ex is TaskCanceledException || ex is OperationCanceledException)
            {
                _logger.LogInformation("Test case was canceled");
                return new TestCaseResult
                {
                    TestCaseName = testCase.Name,
                    Success = false,
                    OutputResults = new List<OutputResult>(),
                    DurationMs = 0,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running test case {TestCase}", testCase.Name);
                return new TestCaseResult
                {
                    TestCaseName = testCase.Name,
                    Success = false,
                    OutputResults = new List<OutputResult>(),
                    DurationMs = 0,
                };
            }
        }

        /// <summary>
        /// Connect to output monitor and get test results
        /// </summary>
        private async Task<TestCaseResult> ConnectToOutputMonitorAsync(
            TestCase testCase,
            DateTime startTime,
            ConcurrentDictionary<string, List<(double Value, long Timestamp)>> inputTimestamps
        )
        {
            try
            {
                // Get output monitor connection details
                string host = _configuration["OutputMonitor:Host"] ?? "localhost";
                int port = _configuration.GetValue<int>("OutputMonitor:Port", 5050);

                _logger.LogInformation("Connecting to Output Monitor at {Host}:{Port}", host, port);

                using var client = new TcpClient();
                await client.ConnectAsync(host, port);

                using var stream = client.GetStream();
                using var reader = new StreamReader(stream);
                using var writer = new StreamWriter(stream) { AutoFlush = true };

                // Prepare the message
                var message = new Dictionary<string, object>
                {
                    ["type"] = "test_start",
                    ["test_case"] = testCase,
                    ["start_time"] = startTime.ToString("o"),
                    ["input_timestamps"] = inputTimestamps,
                };

                // Send message to output monitor
                string json = JsonSerializer.Serialize(message);
                await writer.WriteLineAsync(json);

                // Wait for response
                _logger.LogInformation("Waiting for test results from Output Monitor...");
                string? response = await reader.ReadLineAsync();

                if (string.IsNullOrEmpty(response))
                {
                    _logger.LogWarning("No response received from Output Monitor");
                    return new TestCaseResult
                    {
                        TestCaseName = testCase.Name,
                        Success = false,
                        OutputResults = new List<OutputResult>(),
                        DurationMs = 0,
                    };
                }

                // Deserialize the response
                var result = JsonSerializer.Deserialize<TestCaseResult>(response);

                if (result == null)
                {
                    _logger.LogWarning("Failed to parse response from Output Monitor");
                    return new TestCaseResult
                    {
                        TestCaseName = testCase.Name,
                        Success = false,
                        OutputResults = new List<OutputResult>(),
                        DurationMs = 0,
                    };
                }

                _logger.LogInformation(
                    "Received test results from Output Monitor. Success: {Success}, Avg Latency: {AvgLatency}ms",
                    result.Success,
                    result.AverageLatencyMs
                );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error communicating with Output Monitor");
                return new TestCaseResult
                {
                    TestCaseName = testCase.Name,
                    Success = false,
                    OutputResults = new List<OutputResult>(),
                    DurationMs = 0,
                };
            }
        }
    }
}
