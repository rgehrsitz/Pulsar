// File: Pulsar.Compiler/Config/Templates/Runtime/Services/RedisService.cs

using System.Collections.Concurrent;
using NRedisStack;
using NRedisStack.RedisStackCommands;
using Serilog;
using StackExchange.Redis;
using Polly;
using Polly.Retry;
using Pulsar.Compiler;

namespace Pulsar.Runtime.Services;

public interface IRedisService
{
    Task<Dictionary<string, (double Value, DateTime Timestamp)>> GetSensorValuesAsync(
        IEnumerable<string> sensorKeys
    );
    Task SetOutputValuesAsync(Dictionary<string, double> outputs);
    RedisHealthCheck.ConnectionHealth GetEndpointHealth(string endpoint);
}

public class RedisService : IRedisService, IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, DateTime> _lastErrorTime = new();
    private readonly TimeSpan _errorThrottleWindow = TimeSpan.FromSeconds(60);
    private readonly ConnectionMultiplexer[] _connectionPool;
    private readonly Random _random = new();
    private readonly int _poolSize;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ConfigurationOptions _redisOptions;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly RedisMetrics _metrics;
    private readonly RedisHealthCheck? _healthCheck;
    private bool _disposed;

    public RedisService(RedisConfiguration config, string? logPath = null)
    {
        RedisLoggingConfiguration.EnsureLogDirectories();
        _logger = RedisLoggingConfiguration.ConfigureRedisLogger(config, logPath);
        
        _poolSize = config.PoolSize;
        _connectionPool = new ConnectionMultiplexer[_poolSize];
        _metrics = new RedisMetrics(config.Metrics.InstanceName);
        
        try
        {
            _redisOptions = config.ToRedisOptions();
            
            // Initialize connection pool
            for (int i = 0; i < _poolSize; i++)
            {
                _connectionPool[i] = ConnectionMultiplexer.Connect(_redisOptions);
                SetupConnectionCallbacks(_connectionPool[i]);
            }

            // Configure retry policy with metrics tracking
            _retryPolicy = Policy
                .Handle<RedisConnectionException>()
                .Or<RedisTimeoutException>()
                .WaitAndRetryAsync(
                    config.RetryCount,
                    retryAttempt =>
                    {
                        var delay = TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * config.RetryBaseDelayMs);
                        _logger.Debug(
                            "Retry {RetryAttempt}/{MaxRetries} after {Delay}ms",
                            retryAttempt,
                            config.RetryCount,
                            delay.TotalMilliseconds
                        );
                        return delay;
                    },
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Warning(
                            exception,
                            "Redis operation failed. Retry {RetryCount} after {Delay}ms. Error: {ErrorMessage}",
                            retryCount,
                            timeSpan.TotalMilliseconds,
                            exception.Message
                        );
                        _metrics.TrackError(exception.GetType().Name);
                    }
                );

            // Initialize health check if enabled
            if (config.HealthCheck.Enabled)
            {
                _healthCheck = new RedisHealthCheck(config, _logger);
            }

            _logger.Information(
                "Redis service initialized with pool size: {PoolSize}, endpoints: {Endpoints}",
                _poolSize,
                string.Join(",", config.Endpoints)
            );
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "Failed to initialize Redis service: {ErrorMessage}", ex.Message);
            _metrics?.TrackError("InitializationError");
            throw;
        }
    }

    private void SetupConnectionCallbacks(ConnectionMultiplexer connection)
    {
        connection.ConnectionFailed += (sender, e) =>
        {
            _logger.Error("Redis connection failed: {@Error}", e.Exception);
            _metrics.TrackError("ConnectionFailed");
            if (sender is ConnectionMultiplexer multiplexer)
            {
                var endpoint = multiplexer.Configuration.Split(',')[0];
                _metrics.UpdateConnectionCount(endpoint, (int)multiplexer.GetCounters().TotalOutstanding);
            }
        };

        connection.ConnectionRestored += (sender, e) =>
        {
            _logger.Information("Redis connection restored");
            if (sender is ConnectionMultiplexer multiplexer)
            {
                var endpoint = multiplexer.Configuration.Split(',')[0];
                _metrics.UpdateConnectionCount(endpoint, (int)multiplexer.GetCounters().TotalOutstanding);
            }
        };

        connection.ErrorMessage += (sender, e) =>
            _logger.Warning("Redis error: {Error}", e.Message);

        connection.InternalError += (sender, e) =>
        {
            _logger.Error(e.Exception, "Redis internal error");
            _metrics.TrackError("InternalError");
        };
    }

    private async Task TryReconnect(ConnectionMultiplexer connection)
    {
        try
        {
            if (!connection.IsConnected)
            {
                using var operation = _metrics.TrackOperation("Reconnect");
                await ConnectionMultiplexer.ConnectAsync(_redisOptions);
                _logger.Information("Redis connection reconfigured successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to reconfigure Redis connection");
            _metrics.TrackError("ReconnectionError");
        }
    }

    private ConnectionMultiplexer GetConnection()
    {
        var index = _random.Next(_poolSize);
        var connection = _connectionPool[index];

        if (!connection.IsConnected)
        {
            _logger.Warning("Connection {Index} is disconnected, trying to reconnect", index);
            TryReconnect(connection).Wait();
        }

        return connection;
    }

    public async Task<Dictionary<string, (double Value, DateTime Timestamp)>> GetSensorValuesAsync(
        IEnumerable<string> sensorKeys
    )
    {
        using var operation = _metrics.TrackOperation("GetSensorValues");
        var result = new Dictionary<string, (double Value, DateTime Timestamp)>();
        var keyArray = sensorKeys.ToArray();

        try
        {
            await _connectionLock.WaitAsync();
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();
                var batch = db.CreateBatch();
                var tasks = new List<Task<HashEntry[]>>();

                foreach (var key in keyArray)
                {
                    tasks.Add(batch.HashGetAllAsync(key));
                }

                batch.Execute();
                await Task.WhenAll(tasks);

                for (int i = 0; i < keyArray.Length; i++)
                {
                    var hashValues = tasks[i].Result;
                    var valueEntry = hashValues.FirstOrDefault(he => he.Name == "value");
                    var timestampEntry = hashValues.FirstOrDefault(he => he.Name == "timestamp");

                    if (valueEntry.Value.HasValue && timestampEntry.Value.HasValue)
                    {
                        if (
                            double.TryParse(valueEntry.Value.ToString(), out double value)
                            && long.TryParse(timestampEntry.Value.ToString(), out long ticksValue)
                        )
                        {
                            DateTime timestamp = new DateTime(ticksValue, DateTimeKind.Utc);
                            result[keyArray[i]] = (value, timestamp);
                        }
                        else
                        {
                            LogThrottledWarning(
                                $"Invalid value or timestamp format for sensor {keyArray[i]}"
                            );
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error fetching sensor values from Redis");
            _metrics.TrackError("GetSensorValuesError");
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }

        return result;
    }

    public async Task SetOutputValuesAsync(Dictionary<string, double> outputs)
    {
        if (!outputs.Any())
            return;

        using var operation = _metrics.TrackOperation("SetOutputValues");
        try
        {
            await _connectionLock.WaitAsync();
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();
                var batch = db.CreateBatch();
                var tasks = new List<Task>();
                var timestamp = DateTime.UtcNow.Ticks;

                foreach (var kvp in outputs)
                {
                    tasks.Add(
                        batch.HashSetAsync(
                            kvp.Key,
                            new HashEntry[]
                            {
                                new HashEntry("value", kvp.Value.ToString("G17")),
                                new HashEntry("timestamp", timestamp),
                            }
                        )
                    );
                }

                batch.Execute();
                await Task.WhenAll(tasks);
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing output values to Redis");
            _metrics.TrackError("SetOutputValuesError");
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public RedisHealthCheck.ConnectionHealth GetEndpointHealth(string endpoint)
    {
        return _healthCheck?.GetEndpointHealth(endpoint) 
            ?? new RedisHealthCheck.ConnectionHealth();
    }

    private void LogThrottledWarning(string message)
    {
        if (_lastErrorTime.TryGetValue(message, out var lastTime))
        {
            if (DateTime.UtcNow - lastTime < _errorThrottleWindow)
            {
                return;
            }
        }

        _lastErrorTime.AddOrUpdate(message, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
        _logger.Warning(message);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _logger.Debug("Disposing RedisService");
            _connectionLock.Dispose();
            foreach (var connection in _connectionPool)
            {
                try
                {
                    connection.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Error disposing Redis connection");
                }
            }
            _healthCheck?.Dispose();
            _disposed = true;
        }
    }
}
