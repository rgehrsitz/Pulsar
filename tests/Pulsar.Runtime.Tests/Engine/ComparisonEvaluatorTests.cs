using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.RuleDefinition.Models;
using Pulsar.Runtime.Engine;
using Xunit;

namespace Pulsar.Runtime.Tests.Engine;

public class ComparisonEvaluatorTests
{
    private readonly ComparisonEvaluator _evaluator;
    private readonly Dictionary<string, double> _sensorData;

    public ComparisonEvaluatorTests()
    {
        _evaluator = new ComparisonEvaluator();
        _sensorData = new Dictionary<string, double>
        {
            ["temperature"] = 25.0,
            ["humidity"] = 60.0,
            ["pressure"] = 1013.25
        };
    }

    [Theory]
    [InlineData(">", 20.0, true)]    // 25 > 20 = true
    [InlineData("<", 30.0, true)]    // 25 < 30 = true
    [InlineData(">=", 25.0, true)]   // 25 >= 25 = true
    [InlineData("<=", 25.0, true)]   // 25 <= 25 = true
    [InlineData("==", 25.0, true)]   // 25 == 25 = true
    [InlineData("!=", 20.0, true)]   // 25 != 20 = true
    [InlineData(">", 30.0, false)]   // 25 > 30 = false
    [InlineData("<", 20.0, false)]   // 25 < 20 = false
    public async Task EvaluateAsync_ComparisonOperators_ReturnsExpectedResult(string op, double value, bool expected)
    {
        // Arrange
        var condition = new ComparisonCondition
        {
            Type = "comparison",
            DataSource = "temperature",
            Operator = op,
            Value = value
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _sensorData);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(">", double.MaxValue, false)]  // Test boundary: max value
    [InlineData("<", double.MinValue, false)]  // Test boundary: min value
    [InlineData("==", 25.000000001, false)]    // Test precision comparison
    [InlineData("==", 25.0, true)]             // Test exact equality
    public async Task EvaluateAsync_BoundaryValues_HandledCorrectly(string op, double value, bool expected)
    {
        // Arrange
        var condition = new ComparisonCondition
        {
            Type = "comparison",
            DataSource = "temperature",
            Operator = op,
            Value = value
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _sensorData);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task EvaluateAsync_UnknownDataSource_ThrowsKeyNotFoundException()
    {
        // Arrange
        var condition = new ComparisonCondition
        {
            Type = "comparison",
            DataSource = "unknown_sensor",
            Operator = ">",
            Value = 20.0
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            _evaluator.EvaluateAsync(condition, _sensorData));
    }

    [Fact]
    public async Task EvaluateAsync_EmptySensorData_ThrowsKeyNotFoundException()
    {
        // Arrange
        var condition = new ComparisonCondition
        {
            Type = "comparison",
            DataSource = "temperature",
            Operator = ">",
            Value = 20.0
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            _evaluator.EvaluateAsync(condition, new Dictionary<string, double>()));
    }

    [Fact]
    public async Task EvaluateAsync_InvalidOperator_ThrowsArgumentException()
    {
        // Arrange
        var condition = new ComparisonCondition
        {
            Type = "comparison",
            DataSource = "temperature",
            Operator = "invalid",
            Value = 20.0
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _evaluator.EvaluateAsync(condition, _sensorData));
    }

    [Fact]
    public async Task EvaluateAsync_WrongConditionType_ThrowsArgumentException()
    {
        // Arrange
        var condition = new ThresholdOverTimeCondition
        {
            Type = "threshold_over_time",
            DataSource = "temperature",
            Threshold = 20.0,
            Duration = "5m"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _evaluator.EvaluateAsync(condition, _sensorData));
    }
}
