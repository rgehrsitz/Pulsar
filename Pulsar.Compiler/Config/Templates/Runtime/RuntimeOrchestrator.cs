// File: Pulsar.Compiler/Config/Templates/Runtime/RuntimeOrchestrator.cs
// Version: 1.0.0

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Beacon.Runtime.Interfaces;
using Beacon.Runtime.Models;
using Beacon.Runtime.Services;
using Serilog;

namespace Beacon.Runtime
{
    public class RuntimeOrchestrator
    {
        private readonly IRedisService _redis;
        private readonly ILogger _logger;
        private readonly IRuleCoordinator _coordinator;
        private readonly CancellationTokenSource _cts;
        private readonly MetricsService? _metrics;
        private Task? _executionTask;

        public RuntimeOrchestrator(
            IRedisService redis,
            ILogger logger,
            IRuleCoordinator coordinator,
            MetricsService? metrics = null
        )
        {
            _redis = redis;
            _logger = logger.ForContext<RuntimeOrchestrator>();
            _coordinator = coordinator;
            _metrics = metrics;
            _cts = new CancellationTokenSource();
        }

        public async Task RunCycleAsync()
        {
            try
            {
                // Get all inputs from Redis
                var inputs = await _redis.GetAllInputsAsync();

                // Execute all rules
                var results = await _coordinator.ExecuteRulesAsync(inputs);

                // Store outputs in Redis
                if (results.Count > 0)
                {
                    await _redis.SetOutputsAsync(results);
                    _logger.Information(
                        "Processed {RuleCount} rules with {OutputCount} outputs",
                        _coordinator.RuleCount,
                        results.Count
                    );
                    
                    // Record metrics for output events
                    _metrics?.RecordOutputEvents(results);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing rule cycle");
            }
        }

        public Task StartAsync(int? overrideCycleTimeMs = null, CancellationToken cancellationToken = default)
        {
            if (_executionTask != null)
            {
                _logger.Warning("Runtime orchestrator is already running");
                return Task.CompletedTask;
            }

            // Get cycle time from configuration or use override value
            var config = Models.RuntimeConfig.LoadFromEnvironment();
            var cycleTimeMs = overrideCycleTimeMs ?? (config.TestMode ? config.TestModeCycleTimeMs : config.CycleTime);
            
            if (config.TestMode)
            {
                _logger.Information("Starting runtime orchestrator in TEST MODE with cycle time of {CycleTimeMs}ms", cycleTimeMs);
            }
            else
            {
                _logger.Information("Starting runtime orchestrator with cycle time of {CycleTimeMs}ms", cycleTimeMs);
            }

            // Link the cancellation tokens
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cts.Token,
                cancellationToken
            );

            _executionTask = Task.Run(
                async () =>
                {
                    try
                    {
                        while (!linkedCts.Token.IsCancellationRequested)
                        {
                            var cycleStart = DateTime.UtcNow;
                            await RunCycleAsync();
                            
                            // Calculate time to wait until next cycle
                            var cycleTime = DateTime.UtcNow - cycleStart;
                            var elapsedMs = (int)cycleTime.TotalMilliseconds;
                            var delayMs = Math.Max(0, cycleTimeMs - elapsedMs);
                            
                            // Log the cycle execution time (using Information level for better visibility in demos)
                            _logger.Information("CYCLE STATS: Executed in {ElapsedMs}ms, waiting {DelayMs}ms for next cycle", 
                                elapsedMs, delayMs);
                            
                            // Record metrics for cycle timing
                            _metrics?.RecordCycleTiming(elapsedMs, delayMs);
                            
                            if (delayMs > 0)
                            {
                                await Task.Delay(delayMs, linkedCts.Token);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Information("Runtime orchestrator execution cancelled");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error in runtime orchestrator execution loop");
                    }
                },
                linkedCts.Token
            );

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_executionTask == null)
            {
                _logger.Warning("Runtime orchestrator is not running");
                return Task.CompletedTask;
            }

            _logger.Information("Stopping runtime orchestrator");
            _cts.Cancel();

            return Task.CompletedTask;
        }

        public int RuleCount => _coordinator.RuleCount;
    }
}
