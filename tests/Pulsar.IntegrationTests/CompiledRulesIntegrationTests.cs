using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.CompiledRules;
using Pulsar.Core.Services;
using Pulsar.IntegrationTests.Helpers;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Storage;
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
    private CompiledRuleEngine _ruleEngine = null!;
    private IDatabase _db = null!;

    public CompiledRulesIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _container = new RedisTestContainer(output);
    }

    public async Task InitializeAsync()
    {
        await _container.InitializeAsync();
        _dataStore = _container.GetService<IDataStore>();
        _timeSeriesService = _container.GetService<TimeSeriesService>();
        _db = _container.GetDatabase();

        // Get additional services from container
        var actionExecutor = _container.GetService<IActionExecutor>();
        var logger = _container.GetService<Serilog.ILogger>();

        // Initialize rule engine with container services
        _ruleEngine = new CompiledRuleEngine(_dataStore, actionExecutor, logger);
    }

    private async Task<bool> WaitForAlertValue(string key, string expectedValue, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        _output.WriteLine($"Waiting for alert value {expectedValue} on key {key}...");

        while (sw.Elapsed < timeout)
        {
            var value = await _dataStore.GetValueAsync(key);
            _output.WriteLine($"Current value for {key}: {value}");

            if (value?.ToString() == expectedValue)
                return true;

            await _ruleEngine.ExecuteCycleAsync();
            await Task.Delay(10); // Reduced delay for more frequent checks
        }
        return false;
    }

    [Fact]
    public async Task HighTemperatureAlert_ShouldTrigger_WhenThresholdExceeded()
    {
        // Arrange
        await _db.KeyDeleteAsync("temperature");
        await _db.KeyDeleteAsync("alerts:temperature");

        // Act - Set temperature above threshold (50) and maintain it
        var updateInterval = TimeSpan.FromMilliseconds(10);
        _output.WriteLine("Setting temperature values...");

        for (int i = 0; i < 50; i++) // More updates over a longer period
        {
            await _dataStore.SetValueAsync("temperature", 55.0);
            await Task.Delay(updateInterval);

            if (i % 10 == 0)
            {
                await _ruleEngine.ExecuteCycleAsync();
                _output.WriteLine($"Executed rule cycle after {i + 1} temperature updates");
            }
        }

        // Wait for alert with timeout
        bool alertSet = await WaitForAlertValue("alerts:temperature", "1", TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(alertSet, "Alert was not set within the expected timeframe");
        var alertValue = await _dataStore.GetValueAsync("alerts:temperature");
        Assert.NotNull(alertValue);
        Assert.Equal("1", alertValue.ToString());
    }

    [Fact]
    public async Task TemperatureConversion_ShouldConvertCorrectly()
    {
        // Arrange
        await _db.KeyDeleteAsync("temperature");
        await _db.KeyDeleteAsync("converted_temp");

        // Act - Set temperature (32°F should be 0°C)
        await _dataStore.SetValueAsync("temperature", 32.0);
        await _ruleEngine.ExecuteCycleAsync();

        // Assert
        var convertedTemp = await _dataStore.GetValueAsync("converted_temp");
        Assert.NotNull(convertedTemp);
        Assert.Equal("0", convertedTemp.ToString());
    }

    [Fact]
    public async Task SystemStatusUpdate_ShouldDependOnAlerts()
    {
        // Arrange
        var keys = new[]
        {
            "temperature",
            "humidity",
            "pressure",
            "alerts:temperature",
            "alerts:humidity",
            "alerts:pressure",
            "system:status",
        };
        foreach (var key in keys)
        {
            await _db.KeyDeleteAsync(key);
        }

        // Act - Set values that will trigger alerts
        await _dataStore.SetValueAsync("temperature", 55.0);
        await _dataStore.SetValueAsync("humidity", 25.0);
        await _dataStore.SetValueAsync("pressure", 1100.0);

        // Wait for temporal conditions
        await Task.Delay(600);

        // Execute rules
        await _ruleEngine.ExecuteCycleAsync();

        // Assert
        var systemStatus = await _dataStore.GetValueAsync("system:status");
        Assert.NotNull(systemStatus);
        Assert.Contains("alert", systemStatus.ToString().ToLower());
    }

    public async Task DisposeAsync()
    {
        var keys = new[]
        {
            "temperature",
            "humidity",
            "pressure",
            "alerts:temperature",
            "alerts:humidity",
            "alerts:pressure",
            "system:status",
            "converted_temp",
        };

        foreach (var key in keys)
        {
            await _db.KeyDeleteAsync(key);
        }

        await _container.DisposeAsync();
    }
}
