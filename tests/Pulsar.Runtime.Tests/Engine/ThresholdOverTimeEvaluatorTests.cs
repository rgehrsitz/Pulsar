using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Pulsar.RuleDefinition.Models;
using Pulsar.Runtime.Engine;
using Pulsar.Runtime.Services;
using Serilog;
using Xunit;

namespace Pulsar.Runtime.Tests.Engine;

public class ThresholdOverTimeEvaluatorTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ISensorDataProvider> _mockDataProvider;
    private readonly Mock<IMetricsService> _mockMetrics;
    private readonly ThresholdOverTimeEvaluator _evaluator;
    private readonly Dictionary<string, double> _sensorData;

    public ThresholdOverTimeEvaluatorTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockDataProvider = new Mock<ISensorDataProvider>();
        _mockMetrics = new Mock<IMetricsService>();

        _mockLogger.Setup(l => l.ForContext<ThresholdOverTimeEvaluator>())
            .Returns(_mockLogger.Object);

        _mockLogger.Setup(l => l.ForContext<TimeSeriesService>())
            .Returns(_mockLogger.Object);

        _evaluator = new ThresholdOverTimeEvaluator(
            _mockDataProvider.Object,
            _mockLogger.Object,
            _mockMetrics.Object,
            TimeSpan.FromSeconds(1));

        _sensorData = new Dictionary<string, double>
        {
            ["temperature"] = 26.0
        };
    }

    [Theory]
    [InlineData(ThresholdOperator.LessThan, 25, 24, true)]
    [InlineData(ThresholdOperator.LessThan, 25, 26, false)]
    [InlineData(ThresholdOperator.GreaterThan, 25, 24, false)]
    [InlineData(ThresholdOperator.GreaterThan, 25, 26, true)]
    public async Task EvaluateAsync_WithDifferentOperators_ReturnsExpectedResult(
        ThresholdOperator op,
        double threshold,
        double value,
        bool expected)
    {
        // Arrange
        var condition = new ThresholdOverTimeCondition
        {
            DataSource = "temperature",
            Duration = "5m",
            Operator = op,
            Threshold = threshold
        };

        var sensorData = new Dictionary<string, double>
        {
            ["temperature"] = value
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, sensorData);

        // Assert
        Assert.Equal(expected, result);
        _mockMetrics.Verify(m => m.RecordConditionEvaluation(It.IsAny<string>(), "ThresholdOverTime", result), Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_WithValidCondition_ReturnsExpectedResult()
    {
        // Arrange
        var condition = new ThresholdOverTimeCondition
        {
            DataSource = "temperature",
            Duration = "5m",
            Operator = ThresholdOperator.GreaterThan,
            Threshold = 25.0
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _sensorData);

        // Assert
        Assert.True(result);
        _mockMetrics.Verify(m => m.RecordConditionEvaluation(It.IsAny<string>(), "ThresholdOverTime", true), Times.Once);
    }
}
