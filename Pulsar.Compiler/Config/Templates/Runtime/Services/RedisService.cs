// File: Pulsar.Compiler/Config/Templates/Runtime/Services/RedisService.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Beacon.Runtime.Interfaces;
using Beacon.Runtime.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using StackExchange.Redis;

namespace Beacon.Runtime.Services;

public class RedisService : IRedisService, IDisposable
{
    private readonly ILogger<RedisService> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _lastErrorTime = new();
    private readonly TimeSpan _errorThrottleWindow = TimeSpan.FromSeconds(60);
    private readonly ConnectionMultiplexer[] _connectionPool;
    private readonly Random _random = new();
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ConfigurationOptions _redisOptions;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly RedisMetrics _metrics;
    private readonly RedisHealthCheck? _healthCheck;
    private bool _disposed;

    public bool IsHealthy => _healthCheck?.IsHealthy ?? true;

    public RedisService(RedisConfiguration config, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<RedisService>();

        // Use a default pool size of 5 connections
        var poolSize = 5;
        _connectionPool = new ConnectionMultiplexer[poolSize];
        _metrics = config.Metrics;

        try
        {
            _redisOptions = config.ToRedisOptions();

            // Initialize connection pool
            for (int i = 0; i < poolSize; i++)
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
                        var delay = TimeSpan.FromMilliseconds(
                            Math.Pow(2, retryAttempt) * config.RetryBaseDelayMs
                        );
                        LogDebug($"Retry {retryAttempt}/{config.RetryCount} after {delay.TotalMilliseconds}ms");
                        return delay;
                    },
                    (exception, timeSpan, retryCount, context) =>
                    {
                        LogWarning($"Redis operation failed. Retry {retryCount} after {timeSpan.TotalMilliseconds}ms. Error: {exception.Message}");
                        _metrics.IncrementRetryCount();
                        return Task.CompletedTask;
                    }
                );

            // Initialize health check
            _healthCheck = config.HealthCheck;

            LogInformation($"Redis service initialized with pool size: {poolSize}, endpoints: {string.Join(",", config.Endpoints)}");
        }
        catch (Exception ex)
        {
            LogError($"Failed to initialize Redis service: {ex.Message}", ex);
            _metrics.RecordError("InitializationError");
            throw;
        }
    }

    private void SetupConnectionCallbacks(ConnectionMultiplexer connection)
    {
        connection.ConnectionFailed += (sender, e) =>
        {
            LogError($"Redis connection failed: {e.Exception.Message}", e.Exception);
            _metrics.RecordError("ConnectionFailed");
            if (sender is ConnectionMultiplexer)
            {
                _metrics.UpdateConnectionCount(-1);
            }
        };

        connection.ConnectionRestored += (sender, e) =>
        {
            LogInformation("Redis connection restored");
            if (sender is ConnectionMultiplexer)
            {
                _metrics.UpdateConnectionCount(1);
            }
        };

        connection.ErrorMessage += (sender, e) =>
            LogWarning($"Redis error: {e.Message}");

        connection.InternalError += (sender, e) =>
        {
            LogError($"Redis internal error", e.Exception);
            _metrics.RecordError("InternalError");
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
                LogInformation("Redis connection reconfigured successfully");
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to reconfigure Redis connection", ex);
            _metrics.RecordError("ReconnectionError");
        }
    }

    private ConnectionMultiplexer GetConnection()
    {
        var index = _random.Next(_connectionPool.Length);
        var connection = _connectionPool[index];

        if (!connection.IsConnected)
        {
            LogWarning($"Connection {index} is disconnected, trying to reconnect");
            TryReconnect(connection).Wait();
        }

        return connection;
    }

    // New methods to match the interface
    public async Task<Dictionary<string, object>> GetAllInputsAsync()
    {
        using var operation = _metrics.TrackOperation("GetAllInputs");
        var result = new Dictionary<string, object>();
        
        try
        {
            await _connectionLock.WaitAsync();
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();
                
                // In a real implementation, we would get all keys with a specific pattern or from a specific set
                // For this template, we'll use a simplified approach with a predefined list of sensors
                var sensors = new[] { "input:a", "input:b", "input:c", "temperature", "humidity", "pressure" };
                var batch = db.CreateBatch();
                var tasks = new List<Task<HashEntry[]>>();
                
                foreach (var key in sensors)
                {
                    tasks.Add(batch.HashGetAllAsync(key));
                }
                
                batch.Execute();
                await Task.WhenAll(tasks);
                
                for (int i = 0; i < sensors.Length; i++)
                {
                    var hashValues = tasks[i].Result;
                    var valueEntry = hashValues.FirstOrDefault(he => he.Name == "value");
                    
                    if (valueEntry.Value.HasValue)
                    {
                        if (double.TryParse(valueEntry.Value.ToString(), out double value))
                        {
                            result[sensors[i]] = value;
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            LogError($"Error fetching all inputs from Redis", ex);
            _metrics.RecordError("GetAllInputsError");
        }
        finally
        {
            _connectionLock.Release();
        }
        
        return result;
    }
    
    public async Task SetOutputsAsync(Dictionary<string, object> outputs)
    {
        if (!outputs.Any())
            return;
            
        using var operation = _metrics.TrackOperation("SetOutputs");
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
                    var valueString = kvp.Value.ToString();
                    tasks.Add(
                        batch.HashSetAsync(
                            kvp.Key,
                            new HashEntry[]
                            {
                                new HashEntry("value", valueString),
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
            LogError($"Error writing outputs to Redis", ex);
            _metrics.RecordError("SetOutputsError");
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    
    public async Task<(double Value, DateTime Timestamp)[]> GetValues(string sensor, int count)
    {
        if (count <= 0)
            return Array.Empty<(double, DateTime)>();
            
        using var operation = _metrics.TrackOperation("GetValues");
        var result = new List<(double Value, DateTime Timestamp)>();
        
        try
        {
            await _connectionLock.WaitAsync();
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();
                
                // In a real implementation, we would get historical values from a time series
                // For this template, we'll just get the current value
                var hashValues = await db.HashGetAllAsync(sensor);
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
                        result.Add((value, timestamp));
                    }
                }
            });
        }
        catch (Exception ex)
        {
            LogError($"Error fetching historical values from Redis for sensor {sensor}", ex);
            _metrics.RecordError("GetValuesError");
        }
        finally
        {
            _connectionLock.Release();
        }
        
        // Pad with dummy values if necessary
        while (result.Count < count)
        {
            result.Add((0, DateTime.UtcNow.AddSeconds(-result.Count)));
        }
        
        return result.ToArray();
    }
    
    // Original methods
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
                            LogWarning($"Invalid value or timestamp format for sensor {keyArray[i]}");
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            LogError($"Error fetching sensor values from Redis", ex);
            _metrics.RecordError("GetSensorValuesError");
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
            LogError($"Error writing output values to Redis", ex);
            _metrics.RecordError("SetOutputValuesError");
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private void LogError(string message, Exception? ex = null)
    {
        var now = DateTime.UtcNow;
        var key = message + (ex?.Message ?? string.Empty);
        
        if (_lastErrorTime.TryGetValue(key, out var lastTime) && 
            (now - lastTime) < _errorThrottleWindow)
        {
            return; // Skip logging to avoid flooding logs
        }
        
        _lastErrorTime[key] = now;
        
        if (ex != null)
        {
            _logger.LogError(ex, message);
        }
        else
        {
            _logger.LogError(message);
        }
    }
    
    private void LogDebug(string message)
    {
        _logger.LogDebug(message);
    }
    
    private void LogWarning(string message)
    {
        _logger.LogWarning(message);
    }
    
    private void LogInformation(string message)
    {
        _logger.LogInformation(message);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            LogDebug("Disposing RedisService");
            _connectionLock.Dispose();
            foreach (var connection in _connectionPool)
            {
                try
                {
                    connection.Dispose();
                }
                catch (Exception ex)
                {
                    LogWarning($"Error disposing Redis connection: {ex.Message}");
                }
            }
            _healthCheck?.Dispose();
            _disposed = true;
        }
    }
}
