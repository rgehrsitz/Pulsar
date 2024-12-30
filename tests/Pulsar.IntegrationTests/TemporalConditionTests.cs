using System;
using System.Threading.Tasks;
using Pulsar.IntegrationTests.Helpers;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Storage;
using Xunit;
using Xunit.Abstractions;

namespace Pulsar.IntegrationTests;

public class TemporalConditionTests : IAsyncLifetime
{
    private readonly RedisTestContainer _container;
    private readonly ITestOutputHelper _output;
    private IDataStore _dataStore = null!;
    private TimeSeriesService _timeSeriesService = null!;

    public TemporalConditionTests(ITestOutputHelper output)
    {
        _output = output;
        _container = new RedisTestContainer(output);
    }

    public async Task InitializeAsync()
    {
        await _container.InitializeAsync();
        _dataStore = _container.GetService<IDataStore>();
        _timeSeriesService = _container.GetService<TimeSeriesService>();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "ThresholdTests")]
    public async Task ThresholdOverTime_WhenValuesMaintainedAboveThreshold_ReturnsTrue()
    {
        // Arrange
        string sensorName = "temperature";
        double threshold = 100;
        var duration = TimeSpan.FromMilliseconds(500);

        // Act - Add values above threshold
        for (int i = 0; i < 5; i++)
        {
            await _dataStore.SetValueAsync(sensorName, threshold + 10);
            await Task.Delay(100); // Simulate time passing
        }

        // Assert
        bool result = await _dataStore.CheckThresholdOverTimeAsync(sensorName, threshold, duration);
        Assert.True(result);
    }

    [Fact]
    public async Task ThresholdOverTime_WhenValueDropsBelowThreshold_ReturnsFalse()
    {
        // Arrange
        string sensorName = "temperature";
        double threshold = 100;
        var duration = TimeSpan.FromMilliseconds(500);

        // Act - Add values with one below threshold
        await _dataStore.SetValueAsync(sensorName, threshold + 10);
        await Task.Delay(100);
        await _dataStore.SetValueAsync(sensorName, threshold - 1); // Drop below
        await Task.Delay(100);
        await _dataStore.SetValueAsync(sensorName, threshold + 10);
        await Task.Delay(100);

        // Assert
        bool result = await _dataStore.CheckThresholdOverTimeAsync(sensorName, threshold, duration);
        Assert.False(result);
    }

    [Fact]
    public async Task ThresholdOverTime_WithMultipleSensors_TracksSeparately()
    {
        // Arrange
        string sensor1 = "temp1";
        string sensor2 = "temp2";
        double threshold = 100;
        var duration = TimeSpan.FromMilliseconds(500);

        // Act
        // Sensor 1 stays above threshold
        for (int i = 0; i < 5; i++)
        {
            await _dataStore.SetValueAsync(sensor1, threshold + 10);
            await Task.Delay(100);
        }

        // Sensor 2 drops below threshold
        await _dataStore.SetValueAsync(sensor2, threshold + 10);
        await Task.Delay(100);
        await _dataStore.SetValueAsync(sensor2, threshold - 1);
        await Task.Delay(100);

        // Assert
        bool result1 = await _dataStore.CheckThresholdOverTimeAsync(sensor1, threshold, duration);
        bool result2 = await _dataStore.CheckThresholdOverTimeAsync(sensor2, threshold, duration);

        Assert.True(result1);
        Assert.False(result2);
    }

    [Fact]
    public async Task ThresholdOverTime_WithExpiredData_ReturnsFalse()
    {
        // Arrange
        string sensorName = "temperature";
        double threshold = 100;
        var duration = TimeSpan.FromMilliseconds(200); // Short duration

        // Act
        await _dataStore.SetValueAsync(sensorName, threshold + 10);
        await Task.Delay(300); // Wait longer than duration

        // Assert
        bool result = await _dataStore.CheckThresholdOverTimeAsync(sensorName, threshold, duration);
        Assert.False(result);
    }

    [Fact]
    public async Task ThresholdOverTime_WithHighUpdateFrequency_HandlesDataCorrectly()
    {
        // Arrange
        string sensorName = "high_freq_temp";
        double threshold = 100;
        var duration = TimeSpan.FromMilliseconds(100);
        var updateInterval = TimeSpan.FromMilliseconds(10);

        // Act - Rapidly add values
        for (int i = 0; i < 20; i++)
        {
            await _dataStore.SetValueAsync(sensorName, threshold + 10);
            await Task.Delay(updateInterval);
        }

        // Assert
        bool result = await _dataStore.CheckThresholdOverTimeAsync(sensorName, threshold, duration);
        Assert.True(result);
        Assert.True(_container.Metrics.UpdateCounts[sensorName] >= 10);
    }
}
