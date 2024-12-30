using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.CompiledRules;
using Pulsar.Core.Services;
using Pulsar.IntegrationTests.Helpers;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Storage;
using Pulsar.Runtime.Engine;
using Serilog;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace Pulsar.IntegrationTests;

public class CompiledRulesIntegrationTests : IAsyncLifetime
{
    private readonly RedisTestContainer _container;
    private readonly ITestOutputHelper _output;
    private IDataStore _dataStore = null!;
    private TimeSeriesService _timeSeriesService = null!;
    private readonly ILogger _logger;
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly CompiledRuleEngine _ruleEngine;
    private readonly IActionExecutor _actionExecutor;

    public CompiledRulesIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _container = new RedisTestContainer(output);

        _logger = new LoggerConfiguration()
            .WriteTo.TestOutput(output)
            .CreateLogger();

        // Connect to Redis
        _redis = ConnectionMultiplexer.Connect("localhost:6379");
        _db = _redis.GetDatabase();
            
        // Initialize services
        var metricsService = new TestMetricsService();
        var timeSeriesService = new TimeSeriesService(_logger, metricsService);
        _dataStore = new RedisDataStore(_redis, _logger, timeSeriesService);
        
        // Create action executors
        var sendMessageExecutor = new SendMessageActionExecutor(_logger);
        var setValueExecutor = new SetValueActionExecutor(_logger, _dataStore);
        _actionExecutor = new CompositeActionExecutor(_logger, new IActionExecutor[] { sendMessageExecutor, setValueExecutor });
            
        // Create rule engine
        _ruleEngine = new CompiledRuleEngine(_dataStore, _actionExecutor, _logger);
    }

    public async Task InitializeAsync()
    {
        await _container.InitializeAsync();
        _dataStore = _container.GetService<IDataStore>();
        _timeSeriesService = _container.GetService<TimeSeriesService>();
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

    public async Task DisposeAsync()
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
        await _container.DisposeAsync();
    }
}
