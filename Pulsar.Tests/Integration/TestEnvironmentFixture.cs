// File: Pulsar.Tests/Integration/TestEnvironmentFixture.cs

using System.Diagnostics;
using Pulsar.Compiler;
using Serilog;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Pulsar.Tests.Integration
{
    public class TestEnvironmentFixture : IAsyncLifetime, IDisposable
    {
        private readonly ILogger _logger = LoggingConfig.GetLogger();
        private RedisContainer? _redisContainer;
        private readonly int _maxRetries = 3;
        private readonly int _retryDelayMs = 1000;
        protected IDatabase? _database;

        public TestEnvironmentFixture()
        {
            InitializeTestEnvironment().Wait();
        }

        public async Task InitializeAsync()
        {
            try
            {
                _database = await StartRedisContainer();
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    ex,
                    "Failed to start Redis container. Tests requiring Redis will be skipped."
                );
                // Continue without Redis - tests that need it will be skipped
            }
            await CreateSampleRules();
        }

        private async Task<IDatabase> StartRedisContainer()
        {
            _logger.Information("Starting Redis container...");

            if (_redisContainer == null)
            {
                _redisContainer = new RedisBuilder()
                    .WithImage("redis:latest")
                    .WithPortBinding(6379, true)
                    .Build();
            }

            var retryCount = 0;
            while (true)
            {
                try
                {
                    await _redisContainer.StartAsync();
                    var connection = await ConnectionMultiplexer.ConnectAsync(
                        _redisContainer.GetConnectionString()
                    );
                    _logger.Information("Successfully connected to Redis container");
                    return connection.GetDatabase();
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount >= _maxRetries)
                    {
                        _logger.Error(
                            ex,
                            "Failed to start Redis container after {RetryCount} attempts",
                            _maxRetries
                        );
                        throw new InvalidOperationException(
                            "Redis is required to run integration tests. Please ensure Docker is running and Redis container can be started.",
                            ex
                        );
                    }

                    _logger.Warning(
                        ex,
                        "Failed to start Redis container, attempt {Attempt} of {MaxRetries}. Retrying in {DelayMs}ms...",
                        retryCount,
                        _maxRetries,
                        _retryDelayMs
                    );
                    await Task.Delay(_retryDelayMs);
                }
            }
        }

        public async Task DisposeAsync()
        {
            if (_redisContainer != null)
            {
                await _redisContainer.DisposeAsync();
            }
        }

        public void Dispose()
        {
            if (_redisContainer != null)
            {
                _redisContainer.DisposeAsync().AsTask().Wait();
            }
        }

        public ILogger Logger => _logger;

        public IDatabase? GetDatabase() => _database;

        public async Task GenerateSampleProject(string outputPath)
        {
            var options = new Dictionary<string, string>
            {
                { "rules", "TestData/sample-rules.yaml" },
                { "output", outputPath },
                { "config", Path.Combine("TestData", "system_config.yaml") },
            };

            var result = await Program.GenerateBuildableProject(options, Logger);
            if (!result)
            {
                throw new InvalidOperationException("Failed to generate project");
            }
        }

        public async Task CompileProject(string projectPath)
        {
            var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "publish -c Release -r win-x64 --self-contained true",
                    WorkingDirectory = projectPath,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                }
            );
            await process!.WaitForExitAsync();
        }

        private async Task InitializeTestEnvironment()
        {
            // Create test data directory if it doesn't exist
            Directory.CreateDirectory("TestData");
            Directory.CreateDirectory("TestOutput");

            var options = new Dictionary<string, string> { { "output", "TestData" } };
            var result = Program.InitializeProject(options, Logger).Result;

            // Create system configuration file
            await CreateSystemConfig();
        }

        private async Task CreateSampleRules()
        {
            var sampleRules =
                @"
rules:
  - name: 'TestRule'
    description: 'Simple test rule that copies input to output'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'test:input'
            operator: '>'
            value: 0
    actions:
      - set_value:
          key: 'test:output'
          value_expression: 'test:input'
";
            await File.WriteAllTextAsync("TestData/sample-rules.yaml", sampleRules);
        }

        private async Task CreateSystemConfig()
        {
            // Create system configuration file
            var systemConfig =
                @"
version: 1
validSensors:
  - test:input
  - test:output
cycleTime: 100  # ms
redis:
  endpoints: 
    - localhost:6379
  poolSize: 8
  retryCount: 3
  retryBaseDelayMs: 100
  connectTimeout: 5000
  syncTimeout: 1000
  keepAlive: 60
  password: null
  ssl: false
  allowAdmin: false
bufferCapacity: 100
";
            await File.WriteAllTextAsync(
                Path.Combine("TestData", "system_config.yaml"),
                systemConfig
            );
        }
    }
}
