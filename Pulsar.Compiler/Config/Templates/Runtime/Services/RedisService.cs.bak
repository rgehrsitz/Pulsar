// File: Pulsar.Compiler/Config/Templates/Runtime/Services/RedisService.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Beacon.Runtime.Interfaces;
using Beacon.Runtime.Models;
using Polly;
using Polly.Retry;
using Serilog;
using StackExchange.Redis;

namespace Beacon.Runtime.Services;

public class RedisService : IRedisService, IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, DateTime> _lastErrorTime = new();
    private readonly TimeSpan _errorThrottleWindow = TimeSpan.FromSeconds(60);
    private readonly ConnectionMultiplexer[] _connectionPool;
    private readonly Random _random = new();
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ConfigurationOptions _redisOptions;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly RedisMetrics _redisMetrics;
    private readonly MetricsService? _metrics;
    private readonly RedisHealthCheck? _healthCheck;
    private bool _disposed;

    // Redis key prefixes - standardized domain-prefix naming convention
    private const string INPUT_PREFIX = "input:";
    private const string OUTPUT_PREFIX = "output:";
    private const string STATE_PREFIX = "state:";
    private const string BUFFER_PREFIX = "buffer:";

    public bool IsHealthy => _healthCheck?.IsHealthy ?? true;

    public RedisService(RedisConfiguration config, ILogger logger, MetricsService? metrics = null)
    {
        _logger = logger.ForContext<RedisService>();
        _metrics = metrics;

        // Use a default pool size of 5 connections
        var poolSize = 5;
        _connectionPool = new ConnectionMultiplexer[poolSize];

        // If a specific pool size is configured, use that instead
        if (config.PoolSize > 0)
        {
            poolSize = config.PoolSize;
            _connectionPool = new ConnectionMultiplexer[poolSize];
        }

        _redisOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = config.ConnectTimeout,
            SyncTimeout = config.SyncTimeout,
            Password = config.Password,
            Ssl = config.Ssl,
            AllowAdmin = config.AllowAdmin,
        };

        // Add all endpoints
        foreach (var endpoint in config.Endpoints)
        {
            _redisOptions.EndPoints.Add(endpoint);
        }

        // Configure retry policy
        _retryPolicy = Policy
            .Handle<RedisConnectionException>()
            .Or<RedisTimeoutException>()
            .Or<RedisServerException>()
            .WaitAndRetryAsync(
                config.RetryCount,
                retryAttempt =>
                    TimeSpan.FromMilliseconds(
                        config.RetryBaseDelayMs * Math.Pow(2, retryAttempt - 1)
                    ),
                (ex, timeSpan, retryCount, _) =>
                {
                    // Log the retry but throttle the logging to avoid excessive messages
                    if (!ShouldThrottleError(ex.ToString()))
                    {
                        _logger.Warning(
                            ex,
                            "Redis operation failed, retrying ({RetryCount}/{MaxRetries}) in {DelayMs}ms",
                            retryCount,
                            config.RetryCount,
                            timeSpan.TotalMilliseconds
                        );
                    }
                }
            );

        // Create the metrics
        _redisMetrics = new RedisMetrics();

        // Create the health check
        _healthCheck = new RedisHealthCheck(this, _logger);

        // Create connections in the background
        Task.Run(InitializeConnectionsAsync);
    }

    private async Task InitializeConnectionsAsync()
    {
        try
        {
            await _connectionLock.WaitAsync();
            try
            {
                for (int i = 0; i < _connectionPool.Length; i++)
                {
                    try
                    {
                        _connectionPool[i] = await ConnectionMultiplexer.ConnectAsync(
                            _redisOptions
                        );
                        _logger.Debug("Redis connection {ConnectionNumber} established", i);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            ex,
                            "Failed to establish Redis connection {ConnectionNumber}",
                            i
                        );
                    }
                }
            }
            finally
            {
                _connectionLock.Release();
            }

            _logger.Information(
                "Redis connection pool initialized with {PoolSize} connections",
                _connectionPool.Length
            );
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize Redis connection pool");
        }
    }

    private ConnectionMultiplexer GetConnection()
    {
        // Get a random connection from the pool
        var index = _random.Next(0, _connectionPool.Length);
        var connection = _connectionPool[index];

        // If the connection is null or not connected, try to create a new one
        if (connection == null || !connection.IsConnected)
        {
            _logger.Warning(
                "Redis connection {ConnectionNumber} is not available, attempting to reconnect",
                index
            );

            try
            {
                connection = ConnectionMultiplexer.Connect(_redisOptions);
                _connectionPool[index] = connection;
                _logger.Information("Redis connection {ConnectionNumber} reestablished", index);
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    "Failed to reestablish Redis connection {ConnectionNumber}",
                    index
                );

                // Try to find any working connection in the pool
                for (int i = 0; i < _connectionPool.Length; i++)
                {
                    if (i == index)
                        continue; // Skip the one we just tried

                    var fallbackConnection = _connectionPool[i];
                    if (fallbackConnection != null && fallbackConnection.IsConnected)
                    {
                        _logger.Information(
                            "Using fallback Redis connection {ConnectionNumber}",
                            i
                        );
                        return fallbackConnection;
                    }
                }

                // If we couldn't find a working connection, create a new one synchronously
                // This is a last resort and will block until a connection is established
                _logger.Warning(
                    "No working Redis connections available, creating a new one synchronously"
                );
                connection = ConnectionMultiplexer.Connect(_redisOptions);
                _connectionPool[index] = connection;
            }
        }

        return connection;
    }

    private bool ShouldThrottleError(string errorKey)
    {
        var now = DateTime.UtcNow;
        if (_lastErrorTime.TryGetValue(errorKey, out var lastTime))
        {
            if (now - lastTime < _errorThrottleWindow)
            {
                return true;
            }
        }

        _lastErrorTime[errorKey] = now;
        return false;
    }

    /// <summary>
    /// Gets all input values from Redis
    /// </summary>
    public async Task<Dictionary<string, object>> GetAllInputsAsync()
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();
                var result = new Dictionary<string, object>();

                try
                {
                    // Get both input and output keys for proper rule dependency handling
                    _logger.Debug("Getting all input and output keys from Redis for cycle-aware testing");
                    
                    // Get input keys
                    var inputKeys = await db.ExecuteAsync("KEYS", $"{INPUT_PREFIX}*");
                    if (!inputKeys.IsNull)
                    {
                        await ProcessRedisKeysAsync(db, (RedisResult[])inputKeys, result, INPUT_PREFIX);
                    }
                    
                    // Get output keys (critical for rule dependencies across rule groups)
                    var outputKeys = await db.ExecuteAsync("KEYS", $"{OUTPUT_PREFIX}*");
                    if (!outputKeys.IsNull)
                    {
                        _logger.Debug("Loading {Count} output keys for rule dependencies", ((RedisResult[])outputKeys).Length);
                        await ProcessRedisKeysAsync(db, (RedisResult[])outputKeys, result, OUTPUT_PREFIX);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error getting inputs and outputs from Redis");
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in GetAllInputsAsync");
            throw;
        }
    }
    
    /// <summary>
    /// Helper method to process Redis keys and add their values to the result dictionary
    /// </summary>
    private async Task ProcessRedisKeysAsync(IDatabase db, RedisResult[] keys, Dictionary<string, object> result, string prefix)
    {
        foreach (var key in keys)
        {
            var keyStr = key.ToString();
            try
            {
                // Check key type first to avoid WRONGTYPE errors
                var keyType = await db.KeyTypeAsync(keyStr);
                if (keyType == RedisType.String)
                {
                    var value = await db.StringGetAsync(keyStr);
                    if (!value.IsNull)
                    {
                        // First, add the key with full prefix (e.g., 'input:temperature' or 'output:high_temperature')
                        // This is critical for rule dependencies that explicitly reference prefixed keys
                        if (bool.TryParse(value.ToString(), out var boolValue))
                        {
                            result[keyStr] = boolValue;
                            _logger.Debug("Loaded {KeyType} key: {Key} = {Value}", prefix.TrimEnd(':'), keyStr, boolValue);
                        }
                        else if (double.TryParse(value.ToString(), out var doubleValue))
                        {
                            result[keyStr] = doubleValue;
                            _logger.Debug("Loaded {KeyType} key: {Key} = {Value}", prefix.TrimEnd(':'), keyStr, doubleValue);
                        }
                        else
                        {
                            // Handle string values
                            result[keyStr] = value.ToString();
                            _logger.Debug("Loaded {KeyType} key: {Key} = {Value}", prefix.TrimEnd(':'), keyStr, value.ToString());
                        }

                        // For backward compatibility with input keys only, also add unprefixed version
                        if (prefix == INPUT_PREFIX)
                        {
                            // Extract only the part after the prefix
                            var sensorName = keyStr.Substring(prefix.Length);
                            
                            if (bool.TryParse(value.ToString(), out var boolVal))
                            {
                                result[sensorName] = boolVal;
                            }
                            else if (double.TryParse(value.ToString(), out var doubleVal))
                            {
                                result[sensorName] = doubleVal;
                            }
                            else
                            {
                                result[sensorName] = value.ToString();
                            }
                        }
                            }
                            else
                            {
                                _logger.Warning("Skipping non-string Redis key {Key} of type {Type}", keyStr, keyType);
                            }
                        }
                        catch (Exception innerEx)
                        {
                            _logger.Warning(innerEx, "Failed to process Redis key {Key}", keyStr);
                        }
                    }
                }
                catch (RedisServerException ex) when (ex.Message.Contains("WRONGTYPE"))
                {
                    _logger.Warning("Key type mismatch in Redis. Trying scan command instead.");
                    
                    // Fallback to SCAN command if KEYS fails due to type issues
                    long cursor = 0;
                    do
                    {
                        var scan = await db.ExecuteAsync("SCAN", cursor.ToString(), "MATCH", $"{INPUT_PREFIX}*", "COUNT", "100");
                        var scanResult = (RedisResult[])scan;
                        cursor = long.Parse(scanResult[0].ToString());
                        var keys = (RedisResult[])scanResult[1];
                        
                        foreach (var key in keys)
                        {
                            var keyStr = key.ToString();
                            try 
                            {
                                // Check key type first to avoid WRONGTYPE errors
                                var keyType = await db.KeyTypeAsync(keyStr);
                                if (keyType == RedisType.String)
                                {
                                    var value = await db.StringGetAsync(keyStr);
                                    if (!value.IsNull)
                                    {
                                        // Extract only the part after the prefix
                                        var sensorName = keyStr.Substring(INPUT_PREFIX.Length);
                                        
                                        if (double.TryParse(value, out var doubleValue))
                                        {
                                            // For backward compatibility, include both prefixed and unprefixed keys
                                            result[keyStr] = doubleValue;   // Keep the prefixed key for new code
                                            result[sensorName] = doubleValue; // Keep unprefixed for backward compatibility
                                        }
                                        else
                                        {
                                            // For backward compatibility, include both prefixed and unprefixed keys
                                            result[keyStr] = value.ToString();   // Keep the prefixed key for new code
                                            result[sensorName] = value.ToString(); // Keep unprefixed for backward compatibility
                                        }
                                    }
                                }
                                else
                                {
                                    _logger.Warning("Skipping non-string Redis key {Key} of type {Type}", keyStr, keyType);
                                }
                            }
                            catch (Exception innerEx)
                            {
                                _logger.Warning(innerEx, "Failed to process Redis key {Key}", keyStr);
                            }
                        }
                    } while (cursor != 0);
                }
                
                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get all input values from Redis");
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Sets output values in Redis
    /// </summary>
    public async Task SetOutputsAsync(Dictionary<string, object> outputs)
    {
        if (outputs == null || outputs.Count == 0)
            return;

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();
                var batch = db.CreateBatch();
                var tasks = new List<Task>();

                foreach (var (key, value) in outputs)
                {
                    // Handle case where key might already include the prefix
                    string redisKey;
                    if (key.StartsWith(OUTPUT_PREFIX))
                    {
                        redisKey = key;
                    }
                    else
                    {
                        redisKey = $"{OUTPUT_PREFIX}{key}";
                    }
                    
                    // Special handling for boolean values - use lowercase true/false for proper Redis compatibility
                    string valueStr;
                    if (value is bool boolValue)
                    {
                        valueStr = boolValue.ToString().ToLowerInvariant(); // "true" or "false" lowercase
                        _logger.Debug("Setting boolean key {Key} to {Value}", redisKey, valueStr);
                    }
                    else
                    {
                        valueStr = value.ToString();
                    }
                    
                    tasks.Add(batch.StringSetAsync(redisKey, valueStr));
                }

                batch.Execute();
                await Task.WhenAll(tasks);

                return true;
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set output values in Redis");
        }
    }

    /// <summary>
    /// Gets specific sensor values with their timestamps from Redis
    /// </summary>
    public async Task<Dictionary<string, (double Value, DateTime Timestamp)>> GetSensorValuesAsync(
        IEnumerable<string> sensorKeys
    )
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();
                var result = new Dictionary<string, (double Value, DateTime Timestamp)>();
                var now = DateTime.UtcNow;

                foreach (var key in sensorKeys)
                {
                    try
                    {
                        // Handle case where key might already include the prefix
                        string redisKey;
                        if (key.StartsWith(INPUT_PREFIX))
                        {
                            redisKey = key;
                        }
                        else
                        {
                            redisKey = $"{INPUT_PREFIX}{key}";
                        }
                        
                        var value = await db.StringGetAsync(redisKey);

                        if (!value.IsNull && double.TryParse(value, out var doubleValue))
                        {
                            // Use the original key (without prefix) for the result
                            string resultKey = key;
                            if (key.StartsWith(INPUT_PREFIX))
                            {
                                resultKey = key.Substring(INPUT_PREFIX.Length);
                            }
                            result[resultKey] = (doubleValue, now);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to get value for sensor {SensorKey}", key);
                    }
                }

                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get sensor values from Redis");
            return new Dictionary<string, (double Value, DateTime Timestamp)>();
        }
    }

    /// <summary>
    /// Sets output values in Redis
    /// </summary>
    public async Task SetOutputValuesAsync(Dictionary<string, double> outputs)
    {
        if (outputs == null || outputs.Count == 0)
            return;

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();
                var batch = db.CreateBatch();
                var tasks = new List<Task>();

                foreach (var (key, value) in outputs)
                {
                    // Handle case where key might already include the prefix
                    string redisKey;
                    if (key.StartsWith(OUTPUT_PREFIX))
                    {
                        redisKey = key;
                    }
                    else
                    {
                        redisKey = $"{OUTPUT_PREFIX}{key}";
                    }
                    
                    // Numeric values don't need special handling - convert to string
                    string valueStr = value.ToString();
                    
                    tasks.Add(batch.StringSetAsync(redisKey, valueStr));

                    // Parse key to get the base name without any prefix
                    string baseName = key;
                    if (key.StartsWith(OUTPUT_PREFIX))
                    {
                        baseName = key.Substring(OUTPUT_PREFIX.Length);
                    }
                    
                    // Also store in buffer (historical data)
                    var bufferKey = $"{BUFFER_PREFIX}{baseName}";
                    var entry = $"{DateTime.UtcNow.Ticks}:{value}";
                    tasks.Add(batch.ListRightPushAsync(bufferKey, entry));
                    tasks.Add(batch.ListTrimAsync(bufferKey, 0, 999)); // Keep last 1000 entries
                }

                batch.Execute();
                await Task.WhenAll(tasks);

                return true;
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set output values in Redis");
        }
    }

    /// <summary>
    /// Gets the values for a sensor over time
    /// </summary>
    public async Task<(double Value, DateTime Timestamp)[]> GetValues(string sensor, int count)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();

                // Handle case where sensor might already include the buffer prefix
                string bufferKey;
                if (sensor.StartsWith(BUFFER_PREFIX))
                {
                    bufferKey = sensor;
                }
                else
                {
                    bufferKey = $"{BUFFER_PREFIX}{sensor}";
                }
                
                var entries = await db.ListRangeAsync(bufferKey, -count, -1);

                var result = new List<(double Value, DateTime Timestamp)>();

                foreach (var entry in entries)
                {
                    var parts = entry.ToString().Split(':');
                    if (parts.Length == 2)
                    {
                        if (
                            long.TryParse(parts[0], out var ticks)
                            && double.TryParse(parts[1], out var value)
                        )
                        {
                            var timestamp = new DateTime(ticks, DateTimeKind.Utc);
                            result.Add((value, timestamp));
                        }
                    }
                }

                return result.ToArray();
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get historical values for sensor {SensorKey}", sensor);
            return Array.Empty<(double, DateTime)>();
        }
    }

    public async Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();

                var startTime = DateTime.UtcNow;
                var result = await db.StringSetAsync(key, value, expiry);
                var duration = DateTime.UtcNow - startTime;

                _redisMetrics.RecordOperation("SET", duration);
                _metrics?.RecordRedisConnections(_connectionPool.Count(c => c != null && c.IsConnected));

                return result;
            });
        }
        catch (Exception ex)
        {
            if (!ShouldThrottleError("SET"))
            {
                _logger.Error(ex, "Failed to set Redis key {Key}", key);
            }
            _redisMetrics.RecordError("SET");
            return false;
        }
    }

    public async Task<bool> HashSetAsync(string key, string field, string value)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();

                var startTime = DateTime.UtcNow;
                var result = await db.HashSetAsync(key, field, value);
                var duration = DateTime.UtcNow - startTime;

                _redisMetrics.RecordOperation("HSET", duration);

                return result;
            });
        }
        catch (Exception ex)
        {
            if (!ShouldThrottleError("HSET"))
            {
                _logger.Error(ex, "Failed to set Redis hash field {Key}:{Field}", key, field);
            }
            _redisMetrics.RecordError("HSET");
            return false;
        }
    }

    public async Task<Dictionary<string, string>> HashGetAllAsync(string key)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();

                var startTime = DateTime.UtcNow;
                var hashEntries = await db.HashGetAllAsync(key);
                var duration = DateTime.UtcNow - startTime;

                _redisMetrics.RecordOperation("HGETALL", duration);

                return hashEntries.ToDictionary(
                    he => he.Name.ToString(),
                    he => he.Value.ToString()
                );
            });
        }
        catch (Exception ex)
        {
            if (!ShouldThrottleError("HGETALL"))
            {
                _logger.Error(ex, "Failed to get all Redis hash fields for key {Key}", key);
            }
            _redisMetrics.RecordError("HGETALL");
            return new Dictionary<string, string>();
        }
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var db = connection.GetDatabase();

                var startTime = DateTime.UtcNow;
                var value = await db.StringGetAsync(key);
                var duration = DateTime.UtcNow - startTime;

                _redisMetrics.RecordOperation("GET", duration);

                return value.IsNull ? null : value.ToString();
            });
        }
        catch (Exception ex)
        {
            if (!ShouldThrottleError("GET"))
            {
                _logger.Error(ex, "Failed to get Redis key {Key}", key);
            }
            _redisMetrics.RecordError("GET");
            return null;
        }
    }

    // Add a convenience method for publishing messages to a channel
    public async Task<long> PublishAsync(string channel, string message)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var connection = GetConnection();
                var subscriber = connection.GetSubscriber();

                var startTime = DateTime.UtcNow;
                var result = await subscriber.PublishAsync(channel, message);
                var duration = DateTime.UtcNow - startTime;

                _redisMetrics.RecordOperation("PUBLISH", duration);

                return result;
            });
        }
        catch (Exception ex)
        {
            if (!ShouldThrottleError("PUBLISH"))
            {
                _logger.Error(ex, "Failed to publish message to Redis channel {Channel}", channel);
            }
            _redisMetrics.RecordError("PUBLISH");
            return 0;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var connection in _connectionPool)
        {
            connection?.Dispose();
        }

        _connectionLock.Dispose();
        _disposed = true;
    }
}
