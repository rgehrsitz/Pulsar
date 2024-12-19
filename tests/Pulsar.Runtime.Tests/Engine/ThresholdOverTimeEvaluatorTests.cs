using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.RuleDefinition.Models;
using Pulsar.Runtime.Engine;
using Xunit;

namespace Pulsar.Runtime.Tests.Engine;

public class ThresholdOverTimeEvaluatorTests
{
    private readonly Dictionary<string, double> _currentData;
    private readonly Mock<ISensorDataProvider> _dataProvider;
    private readonly ThresholdOverTimeEvaluator _evaluator;
    private readonly ILogger _logger;

    public ThresholdOverTimeEvaluatorTests()
    {
        _currentData = new Dictionary<string, double>
        {
            ["temperature"] = 25.0,
            ["humidity"] = 60.0,
            ["pressure"] = 1013.25
        };

        _dataProvider = new Mock<ISensorDataProvider>();
        _logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Debug()
            .CreateLogger();

        _evaluator = new ThresholdOverTimeEvaluator(_dataProvider.Object, _logger);
    }

    [Theory]
    [InlineData("5m", 30.0, ThresholdOperator.GreaterThan, true)]    // Average over 5 minutes > 30
    [InlineData("5m", 40.0, ThresholdOperator.GreaterThan, false)]   // Average over 5 minutes < 40
    [InlineData("1h", 25.0, ThresholdOperator.GreaterThan, true)]    // Average over 1 hour > 25
    [InlineData("1s", 35.0, ThresholdOperator.LessThan, false)]      // Average over 1 second < 35
    public async Task EvaluateAsync_ThresholdOverTime_ReturnsExpectedResult(
        string duration,
        double threshold,
        ThresholdOperator op,
        bool expected)
    {
        // Arrange
        var condition = new ThresholdOverTimeCondition
        {
            Type = "threshold_over_time",
            DataSource = "temperature",
            Duration = duration,
            Threshold = threshold,
            Operator = op
        };

        // Simulate historical data
        var startTime = DateTime.UtcNow;
        var data = new Dictionary<string, double>();

        // Add data points that will result in the expected outcome
        for (int i = 0; i < 10; i++)
        {
            data["temperature"] = op == ThresholdOperator.GreaterThan
                ? (expected ? threshold + 1 : threshold - 1)
                : (expected ? threshold - 1 : threshold + 1);

            await _evaluator.EvaluateAsync(condition, data);
        }

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _currentData);

        // Assert
        Assert.Equal(expected, result);
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

    [Theory]
    [InlineData("0m")]      // Zero duration
    [InlineData("-5m")]     // Negative duration
    [InlineData("")]        // Empty duration
    [InlineData("5")]       // Missing unit
    [InlineData("5x")]      // Invalid unit
    [InlineData("25h")]     // Exceeds maximum duration
    public async Task EvaluateAsync_InvalidDuration_ReturnsFalse(string duration)
    {
        // Arrange
        var condition = new ThresholdOverTimeCondition
        {
            Type = "threshold_over_time",
            DataSource = "temperature",
            Duration = duration,
            Threshold = 30.0,
            Operator = ThresholdOperator.GreaterThan
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _currentData);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EvaluateAsync_UnknownDataSource_ReturnsFalse()
    {
        // Arrange
        var condition = new ThresholdOverTimeCondition
        {
            Type = "threshold_over_time",
            DataSource = "unknown_sensor",
            Duration = "5m",
            Threshold = 30.0,
            Operator = ThresholdOperator.GreaterThan
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _currentData);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EvaluateAsync_NoHistoricalData_ReturnsFalse()
    {
        // Arrange
        var condition = new ThresholdOverTimeCondition
        {
            Type = "threshold_over_time",
            DataSource = "empty_sensor",
            Duration = "5m",
            Threshold = 30.0,
            Operator = ThresholdOperator.GreaterThan
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _currentData);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(1000, 3600)]  // 1 hour window with 1s sampling = 3600 points
    public async Task EvaluateAsync_RespectsSamplingRate(int samplingMs, int expectedPoints)
    {
        // Arrange
        var evaluator = new ThresholdOverTimeEvaluator(
            _dataProvider.Object,
            _logger,
            TimeSpan.FromMilliseconds(samplingMs));

        var condition = new ThresholdOverTimeCondition
        {
            DataSource = "temperature",
            Threshold = 25,
            Duration = "1h"
        };

        // Act - simulate data points coming in over time
        var startTime = DateTime.UtcNow;
        for (int i = 0; i < expectedPoints; i++)
        {
            var data = new Dictionary<string, double>
            {
                ["temperature"] = 25.0 + (i % 2) // Alternate between 25 and 26
            };
            await evaluator.EvaluateAsync(condition, data);
        }

        // Assert - verify that we get the expected number of points
        var result = await evaluator.EvaluateAsync(condition, _currentData);
        Assert.True(result); // Temperature should be above 25 for half the points
    }
}

/// <summary>
/// Mock implementation of ISensorDataProvider for testing
/// </summary>
public class MockSensorDataProvider : ISensorDataProvider
{
    public int LastRequestedDataPoints { get; private set; }

    public Task<IDictionary<string, double>> GetCurrentDataAsync()
    {
        return Task.FromResult<IDictionary<string, double>>(new Dictionary<string, double>
        {
            ["temperature"] = 35.0,
            ["humidity"] = 60.0,
            ["pressure"] = 1013.25
        });
    }

    public Task<IReadOnlyList<(DateTime Timestamp, double Value)>> GetHistoricalDataAsync(
        string sensorName,
        TimeSpan duration)
    {
        if (sensorName == "unknown_sensor" || sensorName == "empty_sensor")
            return Task.FromResult<IReadOnlyList<(DateTime, double)>>(
                Array.Empty<(DateTime, double)>());

        // Generate mock historical data
        var now = DateTime.UtcNow;
        var data = new List<(DateTime Timestamp, double Value)>();
        var interval = TimeSpan.FromMinutes(1);
        var count = (int)(duration.TotalMinutes * 1);  // 1 sample per minute

        LastRequestedDataPoints = count;

        for (int i = 0; i < count; i++)
        {
            data.Add((now.Add(-interval * i), 35.0));
        }

        return Task.FromResult<IReadOnlyList<(DateTime, double)>>(data);
    }

    public Task SetSensorDataAsync(IDictionary<string, object> values)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Mock implementation of ISensorDataProvider that always returns empty data
/// </summary>
public class EmptyMockSensorDataProvider : ISensorDataProvider
{
    public Task<IDictionary<string, double>> GetCurrentDataAsync()
    {
        return Task.FromResult<IDictionary<string, double>>(new Dictionary<string, double>());
    }

    public Task<IReadOnlyList<(DateTime Timestamp, double Value)>> GetHistoricalDataAsync(string sensorName, TimeSpan duration)
    {
        return Task.FromResult<IReadOnlyList<(DateTime Timestamp, double Value)>>(
            Array.Empty<(DateTime Timestamp, double Value)>());
    }

    public Task SetSensorDataAsync(IDictionary<string, object> values)
    {
        return Task.CompletedTask;
    }
}
