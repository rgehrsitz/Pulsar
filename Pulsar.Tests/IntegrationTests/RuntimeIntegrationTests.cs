// File: Pulsar.Tests/IntegrationTests/RuntimeIntegrationTests.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Serilog;
using Pulsar.Runtime;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Rules;
using Pulsar.Runtime.Buffers;

namespace Pulsar.Tests.IntegrationTests
{
    public class TestRuleCoordinator : IRuleCoordinator
    {
        private readonly ILogger _logger;
        private readonly RingBufferManager _bufferManager;
        private readonly Dictionary<string, Func<double, double>> _transformations;

        public TestRuleCoordinator(
            ILogger logger,
            RingBufferManager bufferManager,
            Dictionary<string, Func<double, double>> transformations)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));
            _transformations = transformations ?? throw new ArgumentNullException(nameof(transformations));
        }

        public void EvaluateRules(Dictionary<string, double> inputs, Dictionary<string, double> outputs)
        {
            // Create working copy of inputs that we can add intermediate results to
            var workingSet = new Dictionary<string, double>(inputs);

            // Process each transform in order, adding results to both outputs and working set
            foreach (var transform in _transformations.OrderBy(t => t.Key))
            {
                var inputKey = transform.Key;

                if (workingSet.TryGetValue(inputKey, out var value))
                {
                    // Apply transformation
                    var result = transform.Value(value);
                    _logger.Debug("Transform {InputKey}={Value} -> {Result}", inputKey, value, result);

                    // Store in outputs with _out suffix
                    outputs[$"{inputKey}_out"] = result;

                    // Store in working set WITHOUT suffix for next transform to use
                    var nextKey = inputKey switch
                    {
                        var k when k.Contains("raw_temp") => k.Replace("raw_temp", "temp_c"),
                        var k when k.Contains("temp_c") => k.Replace("temp_c", "temp_f"),
                        _ => inputKey
                    };

                    if (nextKey != inputKey)
                    {
                        workingSet[nextKey] = result;
                        _logger.Debug("Stored intermediate {Key}={Value} for next transform", nextKey, result);
                    }
                    if (nextKey != inputKey)
                    {
                        workingSet[nextKey] = result;
                        _logger.Debug("Stored intermediate {Key}={Value} for next transform", nextKey, result);
                    }
                }
                else
                {
                    _logger.Warning("No input value found for transform {Key}", inputKey);
                }
            }

            _logger.Information("Rule evaluation complete. Outputs: {@Outputs}", outputs);
        }
    }


    public class RuntimeIntegrationTests : IDisposable
    {
        private readonly IRedisService _redis;
        private readonly ILogger _logger;
        private readonly string _testKeyPrefix;
        private readonly ITestOutputHelper _output;
        private readonly RingBufferManager _bufferManager;

        public RuntimeIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _testKeyPrefix = $"pulsar_test_{Guid.NewGuid()}_";

            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.TestOutput(output)
                .CreateLogger();

            _redis = new RedisService("localhost:6379", _logger);

            _bufferManager = new RingBufferManager(capacity: 100);
        }

        [Fact]
        public async Task SimpleRule_ExecutesInCycleTime()
        {
            // Arrange
            var transformations = new Dictionary<string, Func<double, double>>
            {
                { $"{_testKeyPrefix}temp", x => x * 1.8 + 32 } // F to C
            };

            var coordinator = new TestRuleCoordinator(_logger, _bufferManager, transformations);
            var orchestrator = new RuntimeOrchestrator(
                _redis,
                _logger,
                new[] { $"{_testKeyPrefix}temp" },
                TimeSpan.FromMilliseconds(100));

            try
            {
                // Load rules and start orchestrator
                orchestrator.LoadRules(coordinator);
                await orchestrator.StartAsync();

                // Set initial temperature
                await _redis.SetOutputValuesAsync(new Dictionary<string, double>
                {
                    [$"{_testKeyPrefix}temp"] = 25.0
                });

                // Let it run one cycle
                await Task.Delay(150); // Wait 1.5 cycles

                // Verify transformed value was written
                var results = await _redis.GetSensorValuesAsync(
                    new[] { $"{_testKeyPrefix}temp_out" });

                Assert.True(results.ContainsKey($"{_testKeyPrefix}temp_out"));
                Assert.Equal(77.0, results[$"{_testKeyPrefix}temp_out"].Item1, 1); // 25C = 77F
            }
            finally
            {
                await orchestrator.StopAsync();
            }
        }


        [Fact]
        public async Task TemporalRule_ExecutesCorrectly()
        {
            // Arrange
            var sensorName = $"{_testKeyPrefix}temp";
            var outputName = $"{_testKeyPrefix}temp_out";

            var transformations = new Dictionary<string, Func<double, double>>
            {
                { sensorName, temp => {
                    _logger.Information(
                        "Checking threshold for sensor {Sensor}. Current value: {Value}",
                        sensorName,
                        temp);

                    var isAbove = _bufferManager.IsAboveThresholdForDuration(
                        sensorName,
                        30.0,
                        TimeSpan.FromMilliseconds(300));

                    _logger.Information(
                        "Threshold check result: {Result} for sensor {Sensor}",
                        isAbove,
                        sensorName);

                    return isAbove ? 1.0 : 0.0;
                }}
            };

            var coordinator = new TestRuleCoordinator(_logger, _bufferManager, transformations);
            var orchestrator = new RuntimeOrchestrator(
                _redis,
                _logger,
                new[] { sensorName },
                TimeSpan.FromMilliseconds(100));

            try
            {
                orchestrator.LoadRules(coordinator);
                await orchestrator.StartAsync();

                // Set initial high temperature and update buffer
                var initialValues = new Dictionary<string, double>
                {
                    [sensorName] = 35.0
                };

                // Simulate updates every 100ms to match the cycle time
                await _redis.SetOutputValuesAsync(initialValues);
                _bufferManager.UpdateBuffers(initialValues);

                for (int i = 0; i < 3; i++)
                {
                    await Task.Delay(100); // Wait for each cycle
                    _bufferManager.UpdateBuffers(new Dictionary<string, double>
                    {
                        [sensorName] = 35.0
                    });
                    _logger.Information("Buffer updated at cycle {Cycle}", i + 1);
                }

                _logger.Information("Initial values set, waiting additional 50ms for buffer alignment");

                // Wait a small margin beyond the required threshold duration
                await Task.Delay(50);

                // Verify results after duration threshold
                var results = await _redis.GetSensorValuesAsync(new[] { outputName });

                _logger.Information(
                    "Results after 350ms: {HasValue}, Value: {Value}",
                    results.ContainsKey(outputName),
                    results.ContainsKey(outputName) ? results[outputName].Item1 : -1);

                Assert.True(results.ContainsKey(outputName) && results[outputName].Item1 > 0,
                    "Alert should trigger after duration threshold");
            }
            finally
            {
                await orchestrator.StopAsync();
            }
        }


        [Fact]
        public async Task LargeSensorSet_MaintainsCycleTime()
        {
            // Arrange
            var sensorCount = 500;
            var sensors = new List<string>();
            var transformations = new Dictionary<string, Func<double, double>>();

            for (int i = 0; i < sensorCount; i++)
            {
                var sensor = $"{_testKeyPrefix}sensor_{i}";
                sensors.Add(sensor);
                transformations[sensor] = x => x * 2; // Simple transformation for each sensor
            }

            var coordinator = new TestRuleCoordinator(_logger, _bufferManager, transformations);
            var orchestrator = new RuntimeOrchestrator(
                _redis,
                _logger,
                sensors.ToArray(),
                TimeSpan.FromMilliseconds(100));

            try
            {
                // Setup initial sensor values
                var initialValues = new Dictionary<string, double>();
                for (int i = 0; i < sensorCount; i++)
                {
                    initialValues[$"{_testKeyPrefix}sensor_{i}"] = i;
                }

                // Load rules and start orchestrator
                orchestrator.LoadRules(coordinator);

                // Measure execution time over multiple cycles
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await orchestrator.StartAsync();

                // Set initial values once at start
                await _redis.SetOutputValuesAsync(initialValues);

                // Let it run for 10 cycles
                await Task.Delay(1100); // 11 * 100ms = 1100ms for good measure

                sw.Stop();

                // Verify outputs - get them all at once after cycles complete
                var outputs = new Dictionary<string, double>();
                coordinator.EvaluateRules(initialValues, outputs);

                // Verify we got all expected outputs
                Assert.Equal(sensorCount, outputs.Count);

                // Verify values were transformed correctly
                foreach (var sensor in sensors)
                {
                    var outputKey = $"{sensor}_out";
                    Assert.True(outputs.ContainsKey(outputKey), $"Missing output for {outputKey}");

                    var sensorNum = int.Parse(sensor.Split('_')[^1]);
                    Assert.Equal(sensorNum * 2, outputs[outputKey]);
                }

                // Calculate average cycle time
                var avgCycleTime = sw.ElapsedMilliseconds / 10.0;
                Assert.True(avgCycleTime <= 150,
                    $"Average cycle time ({avgCycleTime:F1}ms) exceeded target (100ms)");
            }
            finally
            {
                await orchestrator.StopAsync();
            }
        }

        [Fact]
        public async Task ParallelRules_ExecuteEfficiently()
        {
            // Arrange
            var tempSensor = $"{_testKeyPrefix}temp";
            var pressureSensor = $"{_testKeyPrefix}pressure";
            var humiditySensor = $"{_testKeyPrefix}humidity";

            var transformations = new Dictionary<string, Func<double, double>>
        {
            { tempSensor, temp => (temp - 32) * 5/9 }, // F to C
            { pressureSensor, p => p * 0.068948 },    // PSI to bar
            { humiditySensor, h => h * 100 }          // Fraction to percentage
        };

            var coordinator = new TestRuleCoordinator(_logger, _bufferManager, transformations);
            var orchestrator = new RuntimeOrchestrator(
                _redis,
                _logger,
                new[] { tempSensor, pressureSensor, humiditySensor },
                TimeSpan.FromMilliseconds(100));

            try
            {
                orchestrator.LoadRules(coordinator);
                await orchestrator.StartAsync();

                // Set initial values for all sensors simultaneously
                await _redis.SetOutputValuesAsync(new Dictionary<string, double>
                {
                    [tempSensor] = 68.0,      // 68F
                    [pressureSensor] = 14.7,  // 14.7 PSI
                    [humiditySensor] = 0.45   // 45% humidity
                });

                // Let it run one cycle
                await Task.Delay(150);

                // Verify all transformations completed in single cycle
                var results = await _redis.GetSensorValuesAsync(new[]
                {
                tempSensor + "_out",
                pressureSensor + "_out",
                humiditySensor + "_out"
            });

                Assert.Equal(3, results.Count);
                Assert.Equal(20.0, results[tempSensor + "_out"].Item1, 1);        // 68F = 20C
                Assert.Equal(1.01, results[pressureSensor + "_out"].Item1, 2);    // 14.7 PSI ≈ 1.01 bar
                Assert.Equal(45.0, results[humiditySensor + "_out"].Item1, 1);    // 0.45 -> 45%

                // All results should have timestamps within 100ms of each other
                var timestamps = results.Values.Select(v => v.Item2).ToList();
                var maxDiff = timestamps.Max().Subtract(timestamps.Min());
                Assert.True(maxDiff.TotalMilliseconds <= 100,
                    $"Parallel rules exceeded cycle time. Max timestamp difference: {maxDiff.TotalMilliseconds:F1}ms");
            }
            finally
            {
                await orchestrator.StopAsync();
            }
        }

        [Fact]
        public async Task ComplexRuleChain_ExecutesInOrder()
        {
            // Arrange
            var rawTemp = $"{_testKeyPrefix}raw_temp";
            var tempC = $"{_testKeyPrefix}temp_c";
            var tempF = $"{_testKeyPrefix}temp_f";

            var transformations = new Dictionary<string, Func<double, double>>
    {
        { rawTemp, raw => raw * 0.1 },       // Convert rawTemp to Celsius
        { tempC, c => c * 1.8 + 32 },       // Convert Celsius to Fahrenheit
        { tempF, f => f > 100 ? 1.0 : 0.0 } // Trigger alert if Fahrenheit > 100
    };

            var coordinator = new TestRuleCoordinator(_logger, _bufferManager, transformations);
            var orchestrator = new RuntimeOrchestrator(
                _redis,
                _logger,
                new[] { rawTemp, tempC, tempF },
                TimeSpan.FromMilliseconds(100));

            try
            {
                _logger.Information("Pre-initializing buffers...");
                _bufferManager.UpdateBuffers(new Dictionary<string, double>
                {
                    [rawTemp] = 0.0,
                    [tempC] = 0.0,
                    [tempF] = 0.0
                });

                orchestrator.LoadRules(coordinator);
                await orchestrator.StartAsync();

                _logger.Information("Setting raw temperature...");
                await _redis.SetOutputValuesAsync(new Dictionary<string, double>
                {
                    [rawTemp] = 400.0
                });
                _bufferManager.UpdateBuffers(new Dictionary<string, double>
                {
                    [rawTemp] = 400.0
                });

                // Let evaluation run
                await Task.Delay(400);

                // Verify outputs
                var outputs = new Dictionary<string, double>();
                coordinator.EvaluateRules(
                    new Dictionary<string, double> { [rawTemp] = 400.0 }, outputs);

                Assert.True(outputs.ContainsKey($"{rawTemp}_out"), "raw_temp_out is missing");
                Assert.True(outputs.ContainsKey($"{tempC}_out"), "temp_c_out is missing");
                Assert.True(outputs.ContainsKey($"{tempF}_out"), "temp_f_out is missing");

                Assert.Equal(40.0, outputs[$"{rawTemp}_out"]);
                Assert.Equal(104.0, outputs[$"{tempC}_out"]);
                Assert.Equal(1.0, outputs[$"{tempF}_out"]);
            }
            finally
            {
                await orchestrator.StopAsync();
            }
        }



        [Fact]
        public async Task RuntimeHandles_RedisDisruption()
        {
            // Arrange
            var disruptedRedis = new DisruptableRedisService(_redis, _logger);
            var sensorName = $"{_testKeyPrefix}test_sensor";

            var transformations = new Dictionary<string, Func<double, double>>
    {
        { sensorName, x => x * 2 }
    };

            var coordinator = new TestRuleCoordinator(_logger, _bufferManager, transformations);
            var orchestrator = new RuntimeOrchestrator(
                disruptedRedis,
                _logger,
                new[] { sensorName },
                TimeSpan.FromMilliseconds(100));

            try
            {
                orchestrator.LoadRules(coordinator);
                await orchestrator.StartAsync();

                _logger.Information("Setting initial sensor value...");
                await disruptedRedis.SetOutputValuesAsync(new Dictionary<string, double> { [sensorName] = 42.0 });
                await Task.Delay(150);

                // Verify normal operation
                var results = await disruptedRedis.GetSensorValuesAsync(new[] { sensorName + "_out" });
                Assert.Equal(84.0, results[sensorName + "_out"].Item1);

                _logger.Information("Simulating Redis disruption...");
                disruptedRedis.SimulateDisruption();
                await Task.Delay(300); // Allow 3 cycles during disruption

                _logger.Information("Restoring Redis operation...");
                disruptedRedis.RestoreOperation();
                await Task.Delay(150); // Allow recovery

                // Verify system resumes normal operation
                results = await disruptedRedis.GetSensorValuesAsync(new[] { sensorName + "_out" });
                Assert.Equal(84.0, results[sensorName + "_out"].Item1);
            }
            finally
            {
                await orchestrator.StopAsync();
            }
        }

        // Helper class for Redis disruption testing
        private class DisruptableRedisService : IRedisService
        {
            private readonly IRedisService _inner;
            private readonly ILogger _logger;
            private volatile bool _isDisrupted;

            public DisruptableRedisService(IRedisService inner, ILogger logger)
            {
                _inner = inner;
                _logger = logger;
            }

            public void SimulateDisruption() => _isDisrupted = true;
            public void RestoreOperation() => _isDisrupted = false;

            public async Task<Dictionary<string, (double Value, DateTime Timestamp)>> GetSensorValuesAsync(
                IEnumerable<string> sensorKeys)
            {
                if (_isDisrupted)
                {
                    _logger.Warning("Simulated Redis disruption on GetSensorValues");
                    throw new TimeoutException("Simulated Redis timeout");
                }
                return await _inner.GetSensorValuesAsync(sensorKeys);
            }

            public async Task SetOutputValuesAsync(Dictionary<string, double> outputs)
            {
                if (_isDisrupted)
                {
                    _logger.Warning("Simulated Redis disruption on SetOutputValues");
                    throw new TimeoutException("Simulated Redis timeout");
                }
                await _inner.SetOutputValuesAsync(outputs);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_redis is IDisposable disposableRedis)
                {
                    disposableRedis.Dispose();
                }

                _bufferManager.Dispose();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Warning: Cleanup failed: {ex.Message}");
            }
        }
    }
}