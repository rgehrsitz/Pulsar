// File: Pulsar.Compiler/Config/Templates/Runtime/RuntimeOrchestrator.cs
// Version: 1.0.0

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Beacon.Runtime.Interfaces;
using Beacon.Runtime.Services;
using Microsoft.Extensions.Logging;

namespace Beacon.Runtime
{
    public class RuntimeOrchestrator
    {
        private readonly IRedisService _redis;
        private readonly ILogger<RuntimeOrchestrator> _logger;
        private readonly IRuleCoordinator _coordinator;
        private readonly CancellationTokenSource _cts;
        private Task? _executionTask;

        public RuntimeOrchestrator(
            IRedisService redis,
            ILogger<RuntimeOrchestrator> logger,
            IRuleCoordinator coordinator
        )
        {
            _redis = redis;
            _logger = logger;
            _coordinator = coordinator;
            _cts = new CancellationTokenSource();
            
            _logger.LogInformation("RuntimeOrchestrator initialized");
        }

        public async Task StartAsync()
        {
            if (_executionTask != null)
            {
                _logger.LogWarning("RuntimeOrchestrator is already running");
                return;
            }

            _logger.LogInformation("Starting RuntimeOrchestrator");
            _executionTask = Task.Run(() => RunContinuousAsync(_cts.Token));
            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (_executionTask == null)
            {
                _logger.LogWarning("RuntimeOrchestrator is not running");
                return;
            }

            _logger.LogInformation("Stopping RuntimeOrchestrator");
            _cts.Cancel();
            await _executionTask;
            _executionTask = null;
        }

        public async Task RunCycleAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Simplified version for single cycle execution
                var inputs = await _redis.GetAllInputsAsync();
                var outputs = new Dictionary<string, object>();

                // Evaluate rules
                await _coordinator.EvaluateRulesAsync(inputs, outputs);

                // Write outputs back to Redis
                if (outputs.Count > 0)
                {
                    await _redis.SetOutputsAsync(outputs);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("RuntimeOrchestrator execution cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rule evaluation cycle");
                throw;
            }
        }
        
        // This method handles continuous execution with cancellation token
        private async Task RunContinuousAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Get all inputs from Redis
                        var inputs = await _redis.GetAllInputsAsync();
                        var outputs = new Dictionary<string, object>();

                        // Evaluate rules
                        await _coordinator.EvaluateRulesAsync(inputs, outputs);

                        // Write outputs back to Redis
                        if (outputs.Count > 0)
                        {
                            await _redis.SetOutputsAsync(outputs);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during rule evaluation cycle");
                    }

                    await Task.Delay(100, cancellationToken); // Configurable delay
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("RuntimeOrchestrator execution cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in RuntimeOrchestrator");
            }
            finally
            {
                _logger.LogInformation("RuntimeOrchestrator execution stopped");
            }
        }
    }
}
