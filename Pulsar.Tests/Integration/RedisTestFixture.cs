using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Tests.Mocks;
using Pulsar.Tests.TestUtilities;
using StackExchange.Redis;
using Testcontainers.Redis;
// Use fully qualified names to avoid ambiguity
using RedisConfig = Pulsar.Tests.Mocks.RedisConfiguration;

namespace Pulsar.Tests.Integration
{
    public class RedisTestFixture : IAsyncLifetime
    {
        private RedisContainer? _redisContainer;
        private ConnectionMultiplexer? _redisConnection;
        private readonly ILogger _logger = LoggingConfig.GetLogger();

        public RedisService RedisService { get; private set; } = null!;
        public ConnectionMultiplexer Redis =>
            _redisConnection
            ?? throw new InvalidOperationException("Redis connection not initialized");

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Starting Redis container for tests...");

            _redisContainer = new RedisBuilder()
                .WithImage("redis:latest")
                .WithPortBinding(6379, true)
                .Build();

            await _redisContainer.StartAsync();

            var connectionString = _redisContainer.GetConnectionString();
            _redisConnection = await ConnectionMultiplexer.ConnectAsync(connectionString);

            _logger.LogInformation(
                "Connected to Redis test container at {ConnectionString}",
                connectionString
            );

            // Use fully qualified name to avoid ambiguity
            var config = new RedisConfig
            {
                SingleNode = new Mocks.SingleNodeConfig
                {
                    Endpoints = new[] { connectionString },
                    PoolSize = 4,
                    RetryCount = 3,
                    RetryBaseDelayMs = 100,
                    ConnectTimeout = 5000,
                    SyncTimeout = 1000,
                    KeepAlive = 60,
                },
            };

            RedisService = new RedisService(config, new NullLoggerFactory());

            _logger.LogInformation("RedisService initialized for tests");
        }

        public async Task DisposeAsync()
        {
            if (_redisConnection != null)
            {
                _redisConnection.Dispose();
            }

            if (_redisContainer != null)
            {
                _logger.LogInformation("Stopping Redis container...");
                await _redisContainer.StopAsync();
                await _redisContainer.DisposeAsync();
                _logger.LogInformation("Redis container stopped");
            }
        }
    }
}
