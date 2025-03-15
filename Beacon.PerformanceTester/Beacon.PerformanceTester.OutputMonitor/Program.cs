using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Beacon.PerformanceTester.Common;
using Beacon.PerformanceTester.OutputMonitor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Beacon.PerformanceTester.OutputMonitor
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Setup configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
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
                .AddSingleton<IRedisMonitorService, RedisMonitorService>()
                .AddSingleton<IProcessMonitorService, ProcessMonitorService>()
                .AddSingleton<IOutputMonitorService, OutputMonitorService>()
                .BuildServiceProvider();

            // Get the logger
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Beacon Performance Tester - Output Monitor Starting");

            // Setup TCP listener for input generator to communicate with
            var port = configuration.GetValue<int>("Monitor:Port", 5050);
            var listener = new TcpListener(IPAddress.Loopback, port);

            try
            {
                // Get the output monitor service
                var outputMonitor = serviceProvider.GetRequiredService<IOutputMonitorService>();
                await outputMonitor.InitializeAsync();

                // Start TCP listener to receive test information from InputGenerator
                listener.Start();
                logger.LogInformation("Listening for InputGenerator on port {Port}", port);

                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    logger.LogInformation("Connected to InputGenerator");

                    // Read message from client
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream);
                    using var writer = new StreamWriter(stream) { AutoFlush = true };

                    // Handle communication with InputGenerator in a task
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Receive test case info from InputGenerator
                            var message = await reader.ReadLineAsync();
                            if (string.IsNullOrEmpty(message))
                            {
                                logger.LogWarning("Received empty message from InputGenerator");
                                return;
                            }

                            logger.LogDebug("Received message: {Message}", message);

                            // Parse message
                            var messageObj = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                message
                            );
                            if (messageObj == null)
                            {
                                logger.LogWarning("Failed to parse message from InputGenerator");
                                return;
                            }

                            // Handle different message types
                            if (
                                messageObj.TryGetValue("type", out var typeObj)
                                && typeObj.ToString() == "test_start"
                            )
                            {
                                // A test is starting
                                if (
                                    messageObj.TryGetValue("test_case", out var testCaseObj)
                                    && messageObj.TryGetValue("start_time", out var startTimeObj)
                                    && messageObj.TryGetValue(
                                        "input_timestamps",
                                        out var inputTimestampsObj
                                    )
                                )
                                {
                                    // Convert data to appropriate types
                                    var testCaseJson = testCaseObj.ToString();
                                    var testCase = JsonSerializer.Deserialize<TestCase>(
                                        testCaseJson
                                    );
                                    var startTimeStr = startTimeObj.ToString();
                                    var startTime = DateTime.Parse(startTimeStr);

                                    // Deserialize the input timestamps dictionary
                                    var timestampsJson = inputTimestampsObj.ToString();
                                    var inputTimestamps = JsonSerializer.Deserialize<
                                        Dictionary<string, List<(double Value, long Timestamp)>>
                                    >(timestampsJson);

                                    if (testCase != null && inputTimestamps != null)
                                    {
                                        // Start monitoring outputs
                                        logger.LogInformation(
                                            "Starting to monitor test case: {TestCase}",
                                            testCase.Name
                                        );
                                        var result = await outputMonitor.MonitorOutputsAsync(
                                            testCase,
                                            startTime,
                                            inputTimestamps,
                                            CancellationToken.None
                                        );

                                        // Send result back to InputGenerator
                                        var resultJson = JsonSerializer.Serialize(result);
                                        await writer.WriteLineAsync(resultJson);
                                        logger.LogInformation(
                                            "Sent test results back to InputGenerator"
                                        );
                                    }
                                }
                            }
                            else if (
                                messageObj.TryGetValue("type", out var typeObj2)
                                && typeObj2.ToString() == "shutdown"
                            )
                            {
                                // Clean shutdown request
                                logger.LogInformation("Received shutdown request");
                                await outputMonitor.CloseAsync();
                                Environment.Exit(0);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error processing message from InputGenerator");
                        }
                        finally
                        {
                            client.Close();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fatal error in Output Monitor");
            }
            finally
            {
                listener.Stop();
                logger.LogInformation("Output Monitor shut down");

                // Ensure we clean up DI resources
                if (serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
