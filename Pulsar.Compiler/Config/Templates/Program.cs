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
    public interface IRuleCoordinator
    {
        void EvaluateRules(Dictionary<string, double> inputs, Dictionary<string, double> outputs);
        void ProcessInputs(Dictionary<string, string> inputs);
        Dictionary<string, string> GetOutputs();
    }

    internal class TemplateRuleCoordinator : IRuleCoordinator
    {
        private readonly ILogger _logger;
        private readonly RingBufferManager _bufferManager;

        public TemplateRuleCoordinator(ILogger logger, RingBufferManager bufferManager)
        {
            _logger = logger;
            _bufferManager = bufferManager;
        }

        public void EvaluateRules(Dictionary<string, double> inputs, Dictionary<string, double> outputs)
        {
            // Template implementation - no processing needed
        }

        public void ProcessInputs(Dictionary<string, string> inputs)
        {
            // Template implementation - no processing needed
        }

        public Dictionary<string, string> GetOutputs()
        {
            // Template implementation - return empty outputs
            return new Dictionary<string, string>();
        }
    }

    public class ProgramTemplate
    {
        public static async Task<int> Main(string[] args)
        {
            var config = ConfigurationLoader.LoadConfiguration(args);
            var logger = CreateLogger(config);

            try
            {
                logger.Information("Starting Pulsar Runtime v{Version}",
                    typeof(ProgramTemplate).Assembly.GetName().Version);

                using var redis = new Pulsar.Runtime.Services.RedisService(config.RedisConnectionString, logger);
                using var bufferManager = new RingBufferManager(config.BufferCapacity);

                using var orchestrator = new RuntimeOrchestrator(
                    redis,
                    logger,
                    EmbeddedConfig.ValidSensors.ToArray(),
                    new TemplateRuleCoordinator(logger, bufferManager),
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
            // For template purposes, return an empty coordinator
            return new TemplateRuleCoordinator(logger, bufferManager);
        }
    }

    internal static class EmbeddedConfig
    {
        public static string[] ValidSensors { get; } = new string[0];
        public static int CycleTime { get; } = 100;
    }
}
