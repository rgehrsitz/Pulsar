// File: Pulsar.Compiler/Config/Templates/Program.cs

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Buffers;
using Serilog;
using Pulsar.Compiler.Config.Templates.Interfaces;

namespace Pulsar.Runtime.Rules
{
    public class ProgramTemplate
    {
        public static async Task<int> Run(string[] args)
        {
            var config = ConfigurationLoader.LoadConfiguration(args);
            var logger = LoggingConfig.GetLogger();

            try
            {
                logger.Information("Starting Pulsar Runtime v{Version}",
                    typeof(ProgramTemplate).Assembly.GetName().Version);

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

        private static IRuleCoordinator LoadRuleCoordinator(RuntimeConfig config, RingBufferManager bufferManager)
        {
            // For template purposes, return an empty coordinator
            return new TemplateRuleCoordinator(LoggingConfig.GetLogger(), bufferManager);
        }
    }

    internal static class EmbeddedConfig
    {
        public static string[] ValidSensors { get; } = new string[0];
        public static int CycleTime { get; } = 100;
    }
}
