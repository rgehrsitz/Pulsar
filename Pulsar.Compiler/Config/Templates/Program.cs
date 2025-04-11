// File: Pulsar.Compiler/Config/Templates/Program.cs
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Beacon.Runtime.Buffers;
using Beacon.Runtime.Interfaces;
using Beacon.Runtime.Models;
using Beacon.Runtime.Services;
// Removed Generated namespace import
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Beacon.Runtime
{
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(RuntimeConfig))]
    [JsonSerializable(typeof(RedisConfiguration))]
    public partial class SerializationContext : JsonSerializerContext { }

    public class Program
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RuntimeOrchestrator))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RedisService))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RuleCoordinator))]
        public static async Task Main(string[] args)
        {
            // Create a logger factory
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            // Get a logger for the Program class
            var logger = loggerFactory.CreateLogger<Program>();
            logger.LogInformation("Starting Beacon Runtime Engine");
            
            // Parse command line arguments for test mode
            bool testMode = args.Contains("--testmode") || args.Contains("-t");
            int testCycleTimeMs = 0;
            
            // Check for test cycle time
            for (int i = 0; i < args.Length - 1; i++)
            {
                if ((args[i] == "--test-cycle-time" || args[i] == "-c") && int.TryParse(args[i + 1], out int cycleTime))
                {
                    testCycleTimeMs = cycleTime;
                    break;
                }
            }
            
            // Output test mode settings if enabled
            if (testMode)
            {
                logger.LogInformation("Test mode enabled. Cycle time: {CycleTimeMs}ms", testCycleTimeMs > 0 ? testCycleTimeMs : 250);
            }

            try
            {
                // Load configuration
                var config = Models.RuntimeConfig.LoadFromEnvironment();
                
                // Apply test mode settings if specified
                if (testMode)
                {
                    config.TestMode = true;
                    if (testCycleTimeMs > 0)
                    {
                        config.TestModeCycleTimeMs = testCycleTimeMs;
                    }
                }
                logger.LogInformation(
                    "Loaded configuration with {SensorCount} sensors",
                    config.ValidSensors.Count
                );

                // Initialize Redis service
                var redisConfig = config.Redis;

                // Create buffer manager for temporal rules
                var bufferManager = new RingBufferManager(config.BufferCapacity);

                // Initialize metrics service
                var metricsServiceLogger = loggerFactory.CreateLogger<MetricsService>();
                var metricsService = new MetricsService(metricsServiceLogger, Environment.MachineName);
                
                // Start Prometheus metrics server
                metricsService.StartMetricsServer(9090);
                logger.LogInformation("Prometheus metrics available at http://localhost:9090/metrics");
                
                // Initialize runtime orchestrator
                using var redisService = new RedisService(redisConfig, loggerFactory, metricsService);
                var orchestratorLogger = loggerFactory.CreateLogger<RuntimeOrchestrator>();
                var ruleCoordinatorLogger = loggerFactory.CreateLogger<RuleCoordinator>();
                var orchestrator = new RuntimeOrchestrator(
                    redisService,
                    orchestratorLogger,
                    new RuleCoordinator(redisService, ruleCoordinatorLogger, bufferManager),
                    metricsService
                );

                // Start the orchestrator
                await orchestrator.StartAsync();

                // Wait for Ctrl+C
                var cancellationSource = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, e) =>
                {
                    logger.LogInformation("Application shutdown requested");
                    cancellationSource.Cancel();
                    e.Cancel = true;
                };

                // Wait until cancellation is requested
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation was requested
                }

                // Stop the orchestrator
                await orchestrator.StopAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fatal error in Beacon Runtime Engine");
                Environment.ExitCode = 1;
            }
            finally
            {
                logger.LogInformation("Beacon Runtime Engine stopped");
            }
        }
        

    }
}
