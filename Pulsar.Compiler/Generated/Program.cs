// Generated code - do not modify directly
// Generated at: 2025-02-09 23:26:45 UTC

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Prometheus;
using Pulsar.Runtime.Buffers;
using Pulsar.Runtime;
using System.Threading;
using StackExchange.Redis;
using Pulsar.Runtime.Services;

namespace Pulsar.Runtime.Rules
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var config = ConfigurationLoader.LoadConfiguration(args);
            var logger = LoggingConfig.GetLogger();

            try
            {
                logger.Information("Starting Pulsar Runtime v{Version}",
                    typeof(Program).Assembly.GetName().Version);

                using var redis = new RedisService(config.RedisConnectionString);
                using var bufferManager = new RingBufferManager(config.BufferCapacity);

                using var orchestrator = new RuntimeOrchestrator(
                    redis,
                    EmbeddedConfig.ValidSensors.ToArray(),
                    LoadRuleCoordinator(config, bufferManager),
                    config.CycleTime);

                // Setup graceful shutdown
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    logger.Information("Shutdown requested, stopping gracefully...");
                    e.Cancel = true;
                    cts.Cancel();
                };

                logger.Information("Starting orchestrator with {SensorCount} sensors, {CycleTime}ms cycle time",
                    EmbeddedConfig.ValidSensors.Length,
                    config.CycleTime?.TotalMilliseconds ?? 100);

                await orchestrator.StartAsync();

                // Wait for cancellation
                try
                {
                    await Task.Delay(Timeout.Infinite, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                }

                logger.Information("Shutting down...");
                await orchestrator.StopAsync();

                return 0;
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "Fatal error during runtime execution");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IRuleCoordinator LoadRuleCoordinator(RuntimeConfig config, RingBufferManager bufferManager)
        {
            return new RuleCoordinator(LoggingConfig.GetLogger(), bufferManager);
        }
    }
}
