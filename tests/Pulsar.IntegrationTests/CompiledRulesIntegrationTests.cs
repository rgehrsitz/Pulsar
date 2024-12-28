using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Services;
using Pulsar.CompiledRules;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;
using Serilog;

namespace Pulsar.IntegrationTests
{
    public class CompiledRulesIntegrationTests : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly CompiledRuleEngine _ruleEngine;
        private readonly RedisDataStore _dataStore;
        private readonly ActionExecutor _actionExecutor;

        public CompiledRulesIntegrationTests(ITestOutputHelper output)
        {
            _logger = new LoggerConfiguration()
                .WriteTo.TestOutput(output)
                .CreateLogger();

            // Connect to Redis
            _redis = ConnectionMultiplexer.Connect("localhost:6379");
            _db = _redis.GetDatabase();
            
            // Initialize services
            _dataStore = new RedisDataStore(_logger, _redis);
            _actionExecutor = new ActionExecutor(_logger, _redis);
            
            // Create rule engine
            _ruleEngine = new CompiledRuleEngine(_dataStore, _actionExecutor, _logger);
        }

        [Fact]
        public async Task HighTemperatureAlert_ShouldTrigger_WhenThresholdExceeded()
        {
            // Arrange
            await _db.KeyDeleteAsync("temperature");
            await _db.KeyDeleteAsync("alerts:temperature");
            
            // Act - Set temperature above threshold (50)
            await _db.StringSetAsync("temperature", "55.0");
            
            // Wait for the temporal condition (500ms)
            await Task.Delay(600);
            
            // Execute rules
            await _ruleEngine.ExecuteCycleAsync();
            
            // Assert
            var alertValue = await _db.StringGetAsync("alerts:temperature");
            Assert.True(alertValue.HasValue);
            Assert.Equal("1", alertValue.ToString());
        }

        [Fact]
        public async Task TemperatureConversion_ShouldConvertCorrectly()
        {
            // Arrange
            await _db.KeyDeleteAsync("temperature");
            await _db.KeyDeleteAsync("converted_temp");
            
            // Act - Set temperature (32°F should be 0°C)
            await _db.StringSetAsync("temperature", "32.0");
            await _ruleEngine.ExecuteCycleAsync();
            
            // Assert
            var convertedTemp = await _db.StringGetAsync("converted_temp");
            Assert.True(convertedTemp.HasValue);
            Assert.Equal("0", convertedTemp.ToString());
        }

        [Fact]
        public async Task SystemStatusUpdate_ShouldDependOnAlerts()
        {
            // Arrange
            await _db.KeyDeleteAsync("temperature");
            await _db.KeyDeleteAsync("humidity");
            await _db.KeyDeleteAsync("pressure");
            await _db.KeyDeleteAsync("alerts:temperature");
            await _db.KeyDeleteAsync("alerts:humidity");
            await _db.KeyDeleteAsync("alerts:pressure");
            await _db.KeyDeleteAsync("system:status");
            
            // Act - Set values that will trigger alerts
            await _db.StringSetAsync("temperature", "55.0");
            await _db.StringSetAsync("humidity", "25.0"); // Below 30 threshold
            await _db.StringSetAsync("pressure", "1100.0"); // Above normal
            
            // Wait for temporal conditions
            await Task.Delay(600);
            
            // Execute rules
            await _ruleEngine.ExecuteCycleAsync();
            
            // Assert
            var systemStatus = await _db.StringGetAsync("system:status");
            Assert.True(systemStatus.HasValue);
            // System status should indicate multiple alerts are active
            Assert.Contains("alert", systemStatus.ToString().ToLower());
        }

        public async ValueTask DisposeAsync()
        {
            // Clean up Redis keys
            var keys = new[] { "temperature", "humidity", "pressure", "alerts:temperature", 
                "alerts:humidity", "alerts:pressure", "system:status", "converted_temp" };
            
            foreach (var key in keys)
            {
                await _db.KeyDeleteAsync(key);
            }
            
            if (_redis != null)
            {
                await _redis.CloseAsync();
                _redis.Dispose();
            }
        }
    }
}
