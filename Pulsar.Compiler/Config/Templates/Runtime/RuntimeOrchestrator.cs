// File: Pulsar.Compiler/Config/Templates/Runtime/RuntimeOrchestrator.cs
// Version: 1.0.0

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Beacon.Runtime.Interfaces;
using Beacon.Runtime.Services;
using Microsoft.Extensions.Logging;

namespace Beacon.Runtime.Rules
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
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _cts = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            if (_executionTask != null)
            {
                throw new InvalidOperationException("Orchestrator is already running");
            }

            _logger.LogInformation("Starting runtime orchestrator");
            _executionTask = ExecuteRulesAsync(_cts.Token);
            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (_executionTask == null)
            {
                return;
            }

            _logger.LogInformation("Stopping runtime orchestrator");
            _cts.Cancel();
            await _executionTask;
            _executionTask = null;
        }

        private async Task ExecuteRulesAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Get all required sensor values from Redis
                    var sensorValues = await _redis.GetSensorValuesAsync(
                        _coordinator.RequiredSensors
                    );
                    var inputs = new Dictionary<string, object>();
                    var outputs = new Dictionary<string, object>();

                    // Convert sensor values to inputs dictionary
                    foreach (var (sensor, value) in sensorValues)
                    {
                        inputs[sensor] = value;
                    }

                    // Evaluate rules
                    await _coordinator.EvaluateRulesAsync(inputs, outputs);

                    // Convert outputs to doubles for Redis
                    var outputValues = new Dictionary<string, double>();
                    foreach (var (key, value) in outputs)
                    {
                        if (value is double doubleValue)
                        {
                            outputValues[key] = doubleValue;
                        }
                        else if (double.TryParse(value.ToString(), out doubleValue))
                        {
                            outputValues[key] = doubleValue;
                        }
                    }

                    // Write outputs back to Redis
                    if (outputValues.Count > 0)
                    {
                        await _redis.SetOutputValuesAsync(outputValues);
                    }

                    // Wait for next cycle
                    await Task.Delay(100, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during rule evaluation cycle");

                    // Brief delay before retry
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
    }
}
