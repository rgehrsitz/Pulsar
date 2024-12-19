using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.RuleDefinition.Models;
using Pulsar.Runtime.Engine;
using Xunit;

namespace Pulsar.Runtime.Tests.Engine;

public class ThresholdOverTimeEvaluatorTests
{
    private readonly ThresholdOverTimeEvaluator _evaluator;
    private readonly ISensorDataProvider _dataProvider;
    private readonly Dictionary<string, double> _currentData;

    public ThresholdOverTimeEvaluatorTests()
    {
        _currentData = new Dictionary<string, double>
        {
            ["temperature"] = 25.0,
            ["humidity"] = 60.0,
            ["pressure"] = 1013.25
        };

        // Create a mock data provider
        _dataProvider = new MockSensorDataProvider();
        _evaluator = new ThresholdOverTimeEvaluator(_dataProvider);
    }

    [Theory]
    [InlineData("5m", 30.0, true)]   // Average over 5 minutes > 30
    [InlineData("5m", 40.0, false)]  // Average over 5 minutes < 40
    [InlineData("1h", 25.0, true)]   // Average over 1 hour > 25
    public async Task EvaluateAsync_ThresholdOverTime_ReturnsExpectedResult(string duration, double threshold, bool expected)
    {
        // Arrange
        var condition = new ThresholdOverTimeCondition
        {
            Type = "threshold_over_time",
            DataSource = "temperature",
            Duration = duration,
            Threshold = threshold
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _currentData);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task EvaluateAsync_InvalidDuration_ThrowsArgumentException()
    {
        // Arrange
        var condition = new ThresholdOverTimeCondition
        {
            Type = "threshold_over_time",
            DataSource = "temperature",
            Duration = "invalid",
            Threshold = 30.0
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _evaluator.EvaluateAsync(condition, _currentData));
    }

    [Fact]
    public async Task EvaluateAsync_UnknownDataSource_ThrowsKeyNotFoundException()
    {
        // Arrange
        var condition = new ThresholdOverTimeCondition
        {
            Type = "threshold_over_time",
            DataSource = "unknown_sensor",
            Duration = "5m",
            Threshold = 30.0
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            _evaluator.EvaluateAsync(condition, _currentData));
    }

    [Fact]
    public async Task EvaluateAsync_WrongConditionType_ThrowsArgumentException()
    {
        // Arrange
        var condition = new ComparisonCondition
        {
            Type = "comparison",
            DataSource = "temperature",
            Operator = ">",
            Value = 30.0
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _evaluator.EvaluateAsync(condition, _currentData));
    }
}

/// <summary>
/// Mock implementation of ISensorDataProvider for testing
/// </summary>
public class MockSensorDataProvider : ISensorDataProvider
{
    public Task<IDictionary<string, double>> GetCurrentDataAsync()
    {
        var data = new Dictionary<string, double>
        {
            ["temperature"] = 35.0,
            ["humidity"] = 60.0,
            ["pressure"] = 1013.25
        };
        return Task.FromResult<IDictionary<string, double>>(data);
    }

    public Task<IReadOnlyList<(DateTime Timestamp, double Value)>> GetHistoricalDataAsync(string sensorName, TimeSpan duration)
    {
        if (sensorName == "unknown_sensor")
            throw new KeyNotFoundException($"Sensor '{sensorName}' not found");

        // Generate mock historical data
        var now = DateTime.UtcNow;
        var data = new List<(DateTime Timestamp, double Value)>();
        
        for (int i = 0; i < 10; i++)
        {
            data.Add((now.AddMinutes(-i * 6), 35.0));
        }

        return Task.FromResult<IReadOnlyList<(DateTime Timestamp, double Value)>>(data);
    }
}
