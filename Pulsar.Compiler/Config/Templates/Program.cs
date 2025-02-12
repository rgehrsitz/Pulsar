// File: Pulsar.Compiler/Config/Templates/Program.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Prometheus;
using Pulsar.Runtime.Buffers;

using System.Threading;
using StackExchange.Redis;
using Pulsar.Runtime.Rules;
using Pulsar.Runtime.Rules.Services;

namespace Pulsar.Runtime.Rules
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var config = ConfigurationLoader.LoadConfiguration(args);
            var logger = CreateLogger(config);

            try
            {
                logger.Information("Starting Pulsar Runtime v{Version}",
                    typeof(Program).Assembly.GetName().Version);

                using var redis = new RedisService(config.RedisConnectionString, logger);
                using var bufferManager = new RingBufferManager(config.BufferCapacity);

                using var orchestrator = new RuntimeOrchestrator(
                    redis,
                    logger,
                    EmbeddedConfig.ValidSensors.ToArray(),
                    LoadRuleCoordinator(config, logger, bufferManager),
                    null);

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
                    EmbeddedConfig.CycleTime);

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

        private static ILogger CreateLogger(RuntimeConfig config)
        {
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Is(config.LogLevel)
                .WriteTo.Console();

            if (!string.IsNullOrEmpty(config.LogFile))
            {
                loggerConfig.WriteTo.File(config.LogFile);
            }

            return loggerConfig.CreateLogger();
        }

        private static IRuleCoordinator LoadRuleCoordinator(RuntimeConfig config, ILogger logger, RingBufferManager bufferManager)
        {
            return new RuleCoordinator(logger, bufferManager);
        }
    }
}
