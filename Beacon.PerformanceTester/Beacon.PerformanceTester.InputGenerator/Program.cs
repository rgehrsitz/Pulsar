using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Beacon.PerformanceTester.Common;
using Beacon.PerformanceTester.InputGenerator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Beacon.PerformanceTester.InputGenerator
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Setup configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddCommandLine(args)
                .Build();

            // Setup DI
            var serviceProvider = new ServiceCollection()
                .AddLogging(builder =>
                {
                    builder.AddConfiguration(configuration.GetSection("Logging"));
                    builder.AddConsole();
                })
                .AddSingleton<IConfiguration>(configuration)
                .AddSingleton<IPatternGeneratorFactory, PatternGeneratorFactory>()
                .AddSingleton<IRedisDataService, RedisDataService>()
                .AddSingleton<InputGenerator>()
                .BuildServiceProvider();

            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Beacon Performance Tester - Input Generator starting...");

            try
            {
                // Load test scenario from file or command line
                TestScenario scenario;

                string? scenarioFile = configuration["scenarioFile"];
                if (!string.IsNullOrEmpty(scenarioFile))
                {
                    logger.LogInformation("Loading test scenario from {0}", scenarioFile);
                    string json = File.ReadAllText(scenarioFile);
                    scenario =
                        JsonSerializer.Deserialize<TestScenario>(json)
                        ?? throw new InvalidOperationException(
                            "Failed to deserialize test scenario"
                        );
                }
                else
                {
                    // Create a default scenario for testing
                    logger.LogInformation(
                        "No scenario file specified. Using default test scenario"
                    );
                    scenario = CreateDefaultScenario();
                }

                // Run the generator
                var generator = serviceProvider.GetRequiredService<InputGenerator>();
                var cts = new CancellationTokenSource();

                // Handle graceful shutdown
                Console.CancelKeyPress += (s, e) =>
                {
                    logger.LogInformation("Cancellation requested. Shutting down...");
                    cts.Cancel();
                    e.Cancel = true;
                };

                // Run the scenario and get results
                var scenarioResult = await generator.RunScenarioAsync(scenario, cts.Token);

                // Save results to file if outputFile was specified
                string? outputFile = configuration["outputFile"];
                if (!string.IsNullOrEmpty(outputFile))
                {
                    logger.LogInformation("Saving test results to {0}", outputFile);
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string resultJson = JsonSerializer.Serialize(scenarioResult, options);
                    File.WriteAllText(outputFile, resultJson);
                }

                // Print summary to console
                logger.LogInformation(
                    "Scenario Results: {0}",
                    scenarioResult.Success ? "SUCCESS" : "FAILURE"
                );
                logger.LogInformation("Total Duration: {0:F2}ms", scenarioResult.TotalDurationMs);

                foreach (var testResult in scenarioResult.TestCaseResults)
                {
                    logger.LogInformation(
                        "  Test: {0} - {1}",
                        testResult.TestCaseName,
                        testResult.Success ? "PASS" : "FAIL"
                    );
                    logger.LogInformation(
                        "    Average Latency: {0:F2}ms",
                        testResult.AverageLatencyMs
                    );
                    logger.LogInformation("    Max Latency: {0:F2}ms", testResult.MaxLatencyMs);
                    logger.LogInformation("    P95 Latency: {0:F2}ms", testResult.P95LatencyMs);
                    logger.LogInformation("    CPU Usage: {0:F2}%", testResult.AvgCpuPercent);
                    logger.LogInformation("    Memory Usage: {0:F2}MB", testResult.PeakMemoryMB);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while running input generator");
                Environment.ExitCode = 1;
            }
            finally
            {
                logger.LogInformation("Input Generator shutting down");

                // Dispose DI container
                if (serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        private static TestScenario CreateDefaultScenario()
        {
            return new TestScenario
            {
                Name = "Default Performance Test",
                Description = "Basic performance test with temperature and humidity inputs",
                TestCases = new System.Collections.Generic.List<TestCase>
                {
                    new TestCase
                    {
                        Name = "Basic Latency Test",
                        Description = "Simple test with constant inputs",
                        DurationSeconds = 30,
                        Inputs = new System.Collections.Generic.List<SensorConfig>
                        {
                            new SensorConfig
                            {
                                Key = "input:temperature",
                                PatternType = DataPatternType.Constant,
                                ConstantValue = 25,
                                UpdateFrequencyMs = 100,
                            },
                            new SensorConfig
                            {
                                Key = "input:humidity",
                                PatternType = DataPatternType.Constant,
                                ConstantValue = 60,
                                UpdateFrequencyMs = 100,
                            },
                        },
                        ExpectedOutputs = new System.Collections.Generic.List<ExpectedOutput>
                        {
                            new ExpectedOutput
                            {
                                Key = "output:heat_index",
                                Value = 75.69,
                                Tolerance = 0.1,
                                MaxLatencyMs = 100,
                            },
                        },
                    },
                    new TestCase
                    {
                        Name = "Variable Rate Test",
                        Description = "Test with increasing temperature values",
                        DurationSeconds = 60,
                        Inputs = new System.Collections.Generic.List<SensorConfig>
                        {
                            new SensorConfig
                            {
                                Key = "input:temperature",
                                PatternType = DataPatternType.Stepped,
                                MinValue = 20,
                                MaxValue = 35,
                                RateOfChange = 0.1,
                                UpdateFrequencyMs = 50,
                            },
                            new SensorConfig
                            {
                                Key = "input:humidity",
                                PatternType = DataPatternType.Constant,
                                ConstantValue = 60,
                                UpdateFrequencyMs = 100,
                            },
                        },
                        ExpectedOutputs = new System.Collections.Generic.List<ExpectedOutput>
                        {
                            new ExpectedOutput { Key = "output:heat_index", MaxLatencyMs = 100 },
                            new ExpectedOutput
                            {
                                Key = "output:temperature_alert",
                                MaxLatencyMs = 100,
                            },
                        },
                    },
                },
            };
        }
    }
}
