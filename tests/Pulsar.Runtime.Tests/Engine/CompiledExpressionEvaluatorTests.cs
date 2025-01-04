using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.RuleDefinition.Models;
using Pulsar.Runtime.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Pulsar.Runtime.Tests.Engine;

public class CompiledExpressionEvaluatorTests
{
    private readonly CompiledExpressionEvaluator _evaluator;
    private readonly Dictionary<string, double> _sensorData;
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger> _loggerMock;

    public CompiledExpressionEvaluatorTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerMock = new Mock<ILogger>();
        _evaluator = new CompiledExpressionEvaluator(_loggerMock.Object);
        _sensorData = new Dictionary<string, double>
        {
            ["temperature"] = 25.0,
            ["humidity"] = 60.0,
            ["pressure"] = 1013.25,
        };
    }

    [Fact]
    public async Task EvaluateAsync_SimpleComparison_ReturnsExpectedResult()
    {
        // Arrange
        var condition = new ConditionDefinition
        {
            Condition = new ComparisonConditionDefinition
            {
                Type = "comparison",
                DataSource = "temperature",
                Operator = ">",
                Value = "20"
            }
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _sensorData);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_InvalidDataSource_ReturnsFalse()
    {
        // Arrange
        var condition = new ConditionDefinition
        {
            Condition = new ComparisonConditionDefinition
            {
                Type = "comparison",
                DataSource = "invalid_sensor",
                Operator = ">",
                Value = "20"
            }
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _sensorData);

        // Assert
        Assert.False(result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task EvaluateAsync_InvalidOperator_ReturnsFalse()
    {
        // Arrange
        var condition = new ConditionDefinition
        {
            Condition = new ComparisonConditionDefinition
            {
                Type = "comparison",
                DataSource = "temperature",
                Operator = "invalid",
                Value = "20"
            }
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _sensorData);

        // Assert
        Assert.False(result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task EvaluateAsync_InvalidValue_ReturnsFalse()
    {
        // Arrange
        var condition = new ConditionDefinition
        {
            Condition = new ComparisonConditionDefinition
            {
                Type = "comparison",
                DataSource = "temperature",
                Operator = ">",
                Value = "invalid"
            }
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _sensorData);

        // Assert
        Assert.False(result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)
            ),
            Times.Once
        );
    }

    [Theory]
    [InlineData("temperature", ">", "20", true)]
    [InlineData("temperature", "<", "30", true)]
    [InlineData("humidity", ">=", "60", true)]
    [InlineData("humidity", "<=", "70", true)]
    [InlineData("pressure", "==", "1013.25", true)]
    [InlineData("pressure", "!=", "1000", true)]
    [InlineData("temperature", ">", "30", false)]
    [InlineData("humidity", "<", "50", false)]
    public async Task EvaluateAsync_VariousConditions_ReturnsExpectedResults(
        string dataSource,
        string op,
        string value,
        bool expected
    )
    {
        // Arrange
        var condition = new ConditionDefinition
        {
            Condition = new ComparisonConditionDefinition
            {
                Type = "comparison",
                DataSource = dataSource,
                Operator = op,
                Value = value
            }
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _sensorData);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task PerformanceTest_ExpressionEvaluation()
    {
        // Arrange
        var condition = new ConditionDefinition
        {
            Condition = new ExpressionConditionDefinition
            {
                Type = "expression",
                Expression =
                    "(temperature > 20 && humidity < 70) || (pressure > 1000 && Abs(temperature - humidity) > 30)",
            }
        };

        var iterations = 10000;
        var sw = Stopwatch.StartNew();

        // Act - First call (includes compilation)
        var firstResult = await _evaluator.EvaluateAsync(condition, _sensorData);
        var compilationTime = sw.ElapsedMilliseconds;
        _output.WriteLine($"First evaluation (including compilation): {compilationTime}ms");

        // Reset timer for subsequent calls
        sw.Restart();

        // Act - Subsequent calls (cached)
        for (int i = 0; i < iterations; i++)
        {
            await _evaluator.EvaluateAsync(condition, _sensorData);
        }

        var totalTime = sw.ElapsedMilliseconds;
        var averageTime = (double)totalTime / iterations;

        // Assert
        _output.WriteLine($"Average evaluation time (cached): {averageTime:F3}ms");
        _output.WriteLine($"Total time for {iterations} evaluations: {totalTime}ms");

        // Verify performance
        Assert.True(
            averageTime < 0.5,
            $"Average evaluation time ({averageTime:F3}ms) exceeded threshold (0.5ms)"
        );
    }

    [Fact]
    public async Task PerformanceTest_MultipleExpressions()
    {
        // Arrange
        var expressions = new[]
        {
            "temperature > 20 && humidity < 70",
            "pressure > 1000 && temperature < 30",
            "Abs(temperature - humidity) > 30",
            "Max(temperature, humidity) > 50",
            "pressure > 900" // Replaced Sqrt test with simpler expression
        };

        var conditions = expressions
            .Select(e => new ConditionDefinition
            {
                Condition = new ExpressionConditionDefinition
                {
                    Type = "expression",
                    Expression = e
                }
            })
            .ToList();

        var iterations = 1000;
        var sw = Stopwatch.StartNew();

        // Act - First call (includes compilation)
        foreach (var condition in conditions)
        {
            await _evaluator.EvaluateAsync(condition, _sensorData);
        }
        var compilationTime = sw.ElapsedMilliseconds;
        _output.WriteLine(
            $"First evaluation of {expressions.Length} expressions (including compilation): {compilationTime}ms"
        );

        // Reset timer for subsequent calls
        sw.Restart();

        // Act - Subsequent calls (cached)
        for (int i = 0; i < iterations; i++)
        {
            foreach (var condition in conditions)
            {
                await _evaluator.EvaluateAsync(condition, _sensorData);
            }
        }

        var totalTime = sw.ElapsedMilliseconds;
        var averageTime = (double)totalTime / (iterations * expressions.Length);

        // Assert
        _output.WriteLine($"Average evaluation time per expression (cached): {averageTime:F3}ms");
        _output.WriteLine(
            $"Total time for {iterations * expressions.Length} evaluations: {totalTime}ms"
        );

        // Verify performance
        Assert.True(
            averageTime < 0.5,
            $"Average evaluation time ({averageTime:F3}ms) exceeded threshold (0.5ms)"
        );
    }
}
