// File: Pulsar.Compiler/Config/Templates/Runtime/Services/RedisMonitoring.cs

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Serilog;

namespace Beacon.Runtime.Services
{
    public class RedisMetrics
    {
        private readonly Meter _meter;
        private readonly Counter<long> _operationsCounter;
        private readonly Counter<long> _errorsCounter;
        private readonly Histogram<double> _operationDuration;
        private readonly ObservableGauge<int> _connectionPoolSize;
        private readonly ObservableGauge<int> _activeConnections;
        private readonly ConcurrentDictionary<string, int> _activeConnectionsByEndpoint;

        public RedisMetrics(string instanceName)
        {
            _meter = new Meter("Pulsar.Redis", "1.0");
            _activeConnectionsByEndpoint = new ConcurrentDictionary<string, int>();

            _operationsCounter = _meter.CreateCounter<long>(
                "redis.operations",
                "Operations",
                "Total number of Redis operations"
            );

            _errorsCounter = _meter.CreateCounter<long>(
                "redis.errors",
                "Errors",
                "Total number of Redis errors"
            );

            _operationDuration = _meter.CreateHistogram<double>(
                "redis.operation.duration",
                "ms",
                "Duration of Redis operations"
            );

            _connectionPoolSize = _meter.CreateObservableGauge<int>(
                "redis.pool.size",
                () => _activeConnectionsByEndpoint.Count
            );

            _activeConnections = _meter.CreateObservableGauge<int>(
                "redis.connections.active",
                () => _activeConnectionsByEndpoint.Values.Sum()
            );
        }

        public IDisposable TrackOperation(string operationType)
        {
            _operationsCounter.Add(1, new KeyValuePair<string, object?>("type", operationType));
            var sw = Stopwatch.StartNew();
            return new OperationTracker(sw, _operationDuration);
        }

        public void TrackError(string errorType)
        {
            _errorsCounter.Add(1, new KeyValuePair<string, object?>("type", errorType));
        }

        public void UpdateConnectionCount(string endpoint, int count)
        {
            _activeConnectionsByEndpoint.AddOrUpdate(endpoint, count, (_, _) => count);
        }

        private class OperationTracker : IDisposable
        {
            private readonly Stopwatch _stopwatch;
            private readonly Histogram<double> _histogram;

            public OperationTracker(Stopwatch stopwatch, Histogram<double> histogram)
            {
                _stopwatch = stopwatch;
                _histogram = histogram;
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                _histogram.Record(_stopwatch.Elapsed.TotalMilliseconds);
            }
        }
    }

    public class RedisHealthCheck
    {
        private readonly ILogger _logger;
        private readonly AsyncCircuitBreakerPolicy _circuitBreaker;
        private readonly ConcurrentDictionary<string, ConnectionHealth> _endpointHealth;
        private readonly Timer _healthCheckTimer;

        public RedisHealthCheck(RedisConfiguration config, ILogger logger)
        {
            _logger = logger;
            _endpointHealth = new ConcurrentDictionary<string, ConnectionHealth>();

            _circuitBreaker = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (ex, duration) =>
                    {
                        _logger.Warning(
                            ex,
                            "Circuit breaker tripped. Breaking for {Duration}s",
                            duration.TotalSeconds
                        );
                    },
                    onReset: () =>
                    {
                        _logger.Information("Circuit breaker reset");
                    }
                );

            _healthCheckTimer = new Timer(
                CheckHealth,
                null,
                TimeSpan.FromSeconds(0),
                TimeSpan.FromSeconds(config.HealthCheck.HealthCheckIntervalSeconds)
            );

            foreach (var endpoint in config.Endpoints)
            {
                _endpointHealth[endpoint] = new ConnectionHealth();
            }
        }

        private async void CheckHealth(object? state)
        {
            foreach (var endpoint in _endpointHealth.Keys)
            {
                try
                {
                    await _circuitBreaker.ExecuteAsync(async () =>
                    {
                        var health = _endpointHealth[endpoint];
                        var sw = Stopwatch.StartNew();

                        var parts = endpoint.Split(':');
                        using var client = new System.Net.Sockets.TcpClient();
                        await client.ConnectAsync(parts[0], int.Parse(parts[1]));

                        sw.Stop();
                        health.UpdateLatency(sw.Elapsed.TotalMilliseconds);
                        health.MarkSuccess();
                    });
                }
                catch (Exception ex)
                {
                    _endpointHealth[endpoint].MarkFailure();
                    _logger.Error(ex, "Health check failed for endpoint {Endpoint}", endpoint);
                }
            }
        }

        public ConnectionHealth GetEndpointHealth(string endpoint)
        {
            return _endpointHealth.GetValueOrDefault(endpoint, new ConnectionHealth());
        }

        public class ConnectionHealth
        {
            private long _successCount;
            private long _failureCount;
            private double _lastLatency;
            private readonly ConcurrentQueue<double> _latencyHistory;
            private const int MaxHistorySize = 100;

            public ConnectionHealth()
            {
                _latencyHistory = new ConcurrentQueue<double>();
            }

            public void MarkSuccess()
            {
                Interlocked.Increment(ref _successCount);
            }

            public void MarkFailure()
            {
                Interlocked.Increment(ref _failureCount);
            }

            public void UpdateLatency(double latencyMs)
            {
                _lastLatency = latencyMs;
                _latencyHistory.Enqueue(latencyMs);
                while (_latencyHistory.Count > MaxHistorySize)
                {
                    _latencyHistory.TryDequeue(out _);
                }
            }

            public double GetAverageLatency()
            {
                return _latencyHistory.Any() ? _latencyHistory.Average() : 0;
            }

            public double GetSuccessRate()
            {
                var total = _successCount + _failureCount;
                return total > 0 ? (double)_successCount / total : 0;
            }
        }

        public void Dispose()
        {
            _healthCheckTimer?.Dispose();
        }
    }
}
