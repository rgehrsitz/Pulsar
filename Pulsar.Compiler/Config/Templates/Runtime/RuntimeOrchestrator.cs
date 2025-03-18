// File: Pulsar.Compiler/Config/Templates/Runtime/RuntimeOrchestrator.cs
// Version: 1.0.0

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Beacon.Runtime.Interfaces;
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
        private Task? _executionTask;

        public RuntimeOrchestrator(
            IRedisService redis,
            ILogger logger,
            IRuleCoordinator coordinator
        )
        {
            _redis = redis;
            _logger = logger.ForContext<RuntimeOrchestrator>();
            _coordinator = coordinator;
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
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing rule cycle");
            }
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_executionTask != null)
            {
                _logger.Warning("Runtime orchestrator is already running");
                return Task.CompletedTask;
            }

            _logger.Information("Starting runtime orchestrator");

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
                            await RunCycleAsync();
                            await Task.Delay(100, linkedCts.Token); // Default delay
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

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_executionTask == null)
            {
                _logger.Warning("Runtime orchestrator is not running");
                return;
            }

            _logger.Information("Stopping runtime orchestrator");

            try
            {
                _cts.Cancel();
                await _executionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error stopping runtime orchestrator");
            }
            finally
            {
                _executionTask = null;
            }
        }
    }
}
