using Microsoft.Extensions.Logging;
using Pulsar.Tests.TestUtilities;
using Serilog.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Pulsar.Tests.Integration
{
    /// <summary>
    /// Test fixture that manages a Redis container for end-to-end testing of the Beacon executable.
    /// </summary>
    public class EndToEndTestFixture : IAsyncLifetime
    {
        private readonly ILogger<EndToEndTestFixture> _logger = new SerilogLoggerFactory(
            LoggingConfig.GetSerilogLogger()
        ).CreateLogger<EndToEndTestFixture>();
        private RedisContainer _redisContainer;

        public ConnectionMultiplexer Redis { get; private set; }
        public string RedisConnectionString { get; private set; }

        // Create a logger using the existing LoggingConfig utility

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Starting Redis container for end-to-end tests...");

            try
            {
                var builder = new RedisBuilder()
                    .WithImage("redis:latest")
                    .WithPortBinding(6379, true);

                _redisContainer = builder.Build();

                await _redisContainer.StartAsync();

                // Modify the connection string to ensure proper format
                // TestContainers may return a connection string with host and port separated differently than what Beacon expects
                var endpoint =
                    _redisContainer.Hostname + ":" + _redisContainer.GetMappedPublicPort(6379);
                RedisConnectionString = endpoint;
                _logger.LogInformation(
                    "Redis container started with connection string: {ConnectionString}",
                    RedisConnectionString
                );

                // For testing without Docker, use a static connection string
                if (string.IsNullOrEmpty(RedisConnectionString))
                {
                    RedisConnectionString = "localhost:6379";
                    _logger.LogWarning(
                        "Using fallback Redis connection: {ConnectionString}",
                        RedisConnectionString
                    );
                }

                // Configure connection with retry options
                var options = ConfigurationOptions.Parse(RedisConnectionString);
                options.AbortOnConnectFail = false;
                options.ConnectRetry = 5;
                options.ConnectTimeout = 10000;

                Redis = await ConnectionMultiplexer.ConnectAsync(options);
                _logger.LogInformation("Connected to Redis test container");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to start Redis container. Using local Redis if available."
                );

                // Try to connect to a local Redis instance as fallback
                try
                {
                    RedisConnectionString = "localhost:6379";
                    _logger.LogWarning(
                        "Attempting to connect to local Redis at: {ConnectionString}",
                        RedisConnectionString
                    );

                    // Configure connection with retry options
                    var options = ConfigurationOptions.Parse(RedisConnectionString);
                    options.AbortOnConnectFail = false;
                    options.ConnectRetry = 3;
                    options.ConnectTimeout = 5000;

                    Redis = await ConnectionMultiplexer.ConnectAsync(options);
                    _logger.LogInformation("Connected to local Redis instance");
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(
                        fallbackEx,
                        "Failed to connect to local Redis instance as fallback. Tests requiring Redis will fail."
                    );
                    throw;
                }
            }
        }

        public async Task DisposeAsync()
        {
            if (Redis != null)
            {
                Redis.Dispose();
                _logger.LogInformation("Redis connection disposed");
            }

            if (_redisContainer != null)
            {
                _logger.LogInformation("Stopping Redis container...");
                await _redisContainer.StopAsync();
                await _redisContainer.DisposeAsync();
                _logger.LogInformation("Redis container stopped");
            }
        }

        /// <summary>
        /// Clears all keys in the Redis database to ensure a clean state between tests.
        /// </summary>
        public async Task ClearRedisAsync()
        {
            try
            {
                if (Redis == null || !Redis.IsConnected)
                {
                    _logger.LogWarning("Redis is not connected. Cannot clear keys.");
                    return;
                }

                var db = Redis.GetDatabase();

                // Get a server endpoint to use for listing keys
                var serverEndPoint = Redis.GetEndPoints().FirstOrDefault();
                if (serverEndPoint == null)
                {
                    _logger.LogWarning("No Redis server endpoints available. Cannot clear keys.");
                    return;
                }

                var server = Redis.GetServer(serverEndPoint);

                // Use pattern to get all keys and delete them
                try
                {
                    // Get all keys and delete them
                    foreach (var key in server.Keys(pattern: "*"))
                    {
                        await db.KeyDeleteAsync(key);
                    }

                    _logger.LogInformation("Cleared all keys from Redis");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing Redis keys. Continuing test.");
                    // Don't throw the exception - allow the test to continue
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing Redis. Continuing test without clearing.");
                // Don't throw the exception - allow the test to continue
            }
        }

        /// <summary>
        /// Simulates a Redis failure by stopping the container.
        /// </summary>
        public async Task SimulateRedisFailureAsync()
        {
            _logger.LogInformation("Simulating Redis failure by stopping container...");
            await _redisContainer.StopAsync();
            _logger.LogInformation("Redis container stopped to simulate failure");
        }

        /// <summary>
        /// Restores the Redis connection by restarting the container.
        /// </summary>
        public async Task RestoreRedisConnectionAsync()
        {
            _logger.LogInformation("Restoring Redis connection by starting container...");
            await _redisContainer.StartAsync();

            // Reconnect
            if (Redis != null)
            {
                Redis.Dispose();
            }

            RedisConnectionString = _redisContainer.GetConnectionString();
            Redis = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
            _logger.LogInformation("Redis connection restored");
        }

        /// <summary>
        /// Gets a standardized system configuration YAML for testing.
        /// </summary>
        public string GetSystemConfigYaml()
        {
            string connectionString = RedisConnectionString;

            // Ensure Redis connection string is properly formatted for YAML
            if (connectionString.Contains(':') || connectionString.Contains('/'))
            {
                // If the connection string contains special characters, wrap it in quotes
                connectionString = $"\"{connectionString}\"";
            }

            return @"version: 1
validSensors:
  - input:temperature
  - input:humidity
  - input:pressure
  - input:status
  - input:mode
  - input:system
  - output:high_temperature
  - output:temperature_rising
  - output:high_temp_and_humidity
  - output:normalized_temp
  - output:temp_alert_level
  - output:heat_index
  - output:status_active
  - output:status_message
  - output:alert_condition
  - output:alert_system
  - output:system_ready
  - output:minimal_executed
  - buffer:temp_history
cycleTime: 100
redis:
  endpoints:
    - " + connectionString + @"
  poolSize: 4
  retryCount: 3
  retryBaseDelayMs: 100
  connectTimeout: 5000
  syncTimeout: 1000
  keepAlive: 60
  password: null
  ssl: false
  allowAdmin: false
bufferCapacity: 100";
        }
    }
}