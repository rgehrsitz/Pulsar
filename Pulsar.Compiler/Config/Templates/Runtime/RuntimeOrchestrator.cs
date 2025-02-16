// File: Pulsar.Compiler/Config/Templates/Runtime/RuntimeOrchestrator.cs
// Version: 1.0.0

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Pulsar.Runtime.Buffers;
using Pulsar.Runtime.Services;
using Serilog;
using Pulsar.Compiler;
using Pulsar.Compiler.Config.Templates.Interfaces;

namespace Pulsar.Runtime.Rules
{
    public interface IRuntimeOrchestrator
    {
        Task StartAsync();
        Task StopAsync();
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Types preserved in trimming.xml"
    )]
    public class RuntimeOrchestrator : IRuntimeOrchestrator, IDisposable
    {
        private readonly IRedisService _redis;
        private readonly ILogger _logger;
        private readonly string[] _requiredSensors;
        private readonly CancellationTokenSource _cts;
        private readonly object _rulesLock = new();
        private readonly PeriodicTimer _timer;
        private readonly TimeSpan _cycleTime;
        private readonly RingBufferManager _bufferManager;
        private readonly IRuleCoordinator _ruleCoordinator;

        private bool _disposed;
        private DateTime _lastWarningTime = DateTime.MinValue;
        private Task? _executionTask;

        public RuntimeOrchestrator(
            IRedisService redis,
            string[] requiredSensors,
            IRuleCoordinator ruleCoordinator,
            TimeSpan? cycleTime = null,
            int bufferCapacity = 100
        )
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = LoggingConfig.GetLogger();
            _requiredSensors = requiredSensors ?? throw new ArgumentNullException(nameof(requiredSensors));
            _ruleCoordinator = ruleCoordinator ?? throw new ArgumentNullException(nameof(ruleCoordinator));

            _cycleTime = cycleTime ?? TimeSpan.FromMilliseconds(100);
            _timer = new PeriodicTimer(_cycleTime);
            _cts = new CancellationTokenSource();
            _bufferManager = new RingBufferManager(bufferCapacity);

            _logger.Information(
                "Runtime orchestrator initialized with {SensorCount} sensors, {CycleTime}ms cycle time, and {BufferCapacity} buffer capacity",
                requiredSensors.Length,
                _cycleTime.TotalMilliseconds,
                bufferCapacity
            );
        }

        public async Task StartAsync()
        {
            _executionTask = ExecutionLoop();
            _logger.Information("Runtime execution started");
            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            _cts.Cancel();
            if (_executionTask != null)
            {
                await _executionTask;
            }
            _logger.Information("Runtime execution stopped");
        }

        private async Task ExecutionLoop()
        {
            try
            {
                while (await _timer.WaitForNextTickAsync(_cts.Token))
                {
                    try
                    {
                        await ExecuteCycleAsync();
                    }
                    catch (TimeoutException ex)
                    {
                        _logger.Warning(
                            ex,
                            "Redis timeout during execution cycle. Skipping this cycle."
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.Fatal(ex, "Fatal error in execution loop");
                        throw;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Execution loop cancelled");
            }
        }

        public async Task ExecuteCycleAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(RuntimeOrchestrator),
                    "Cannot call ExecuteCycleAsync after the orchestrator has been disposed."
                );
            }

            var cycleStart = DateTime.UtcNow;
            var outputs = new Dictionary<string, double>();

            try
            {
                // Get all sensor values with timestamps in bulk
                var sensorData = await _redis.GetSensorValuesAsync(_requiredSensors);

                // Convert to format needed for rules evaluation while preserving timestamps
                var inputs = sensorData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);

                // Update ring buffers with timestamps from Redis
                _bufferManager.UpdateBuffers(inputs);

                // Execute rules with access to both current values and buffer manager
                lock (_rulesLock)
                {
                    _ruleCoordinator.EvaluateRules(inputs, outputs);
                }

                // Write outputs with current timestamp
                if (outputs.Any())
                {
                    await _redis.SetOutputValuesAsync(outputs);
                }

                // Check cycle time
                var cycleTime = DateTime.UtcNow - cycleStart;
                if (cycleTime > _cycleTime && DateTime.UtcNow - _lastWarningTime > TimeSpan.FromMinutes(1))
                {
                    _logger.Warning(
                        "Cycle time ({ActualMs}ms) exceeded target ({TargetMs}ms)",
                        cycleTime.TotalMilliseconds,
                        _cycleTime.TotalMilliseconds
                    );
                    _lastWarningTime = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during execution cycle");
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _timer.Dispose();
            _cts.Dispose();

            if (_redis is IDisposable disposableRedis)
            {
                disposableRedis.Dispose();
            }

            _bufferManager.Clear();
            _disposed = true;
        }
    }
}
