using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
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

    public CompiledExpressionEvaluatorTests(ITestOutputHelper output)
    {
        _output = output;
        _evaluator = new CompiledExpressionEvaluator();
        _sensorData = new Dictionary<string, double>
        {
            ["temperature"] = 25.0,
            ["humidity"] = 60.0,
            ["pressure"] = 1013.25
        };
    }

    [Theory]
    [InlineData("temperature > 20", true)]               // Simple comparison
    [InlineData("temperature < 20", false)]              // Simple comparison (false)
    [InlineData("humidity >= 50 && humidity <= 70", true)]     // Range check
    [InlineData("temperature * 2 > 45", true)]           // Arithmetic
    [InlineData("(temperature > 20) && (humidity < 70)", true)]  // Logical AND
    [InlineData("(temperature > 30) || (humidity < 70)", true)]  // Logical OR
    [InlineData("pressure > 1000 && pressure < 1020", true)]     // Complex condition
    public async Task EvaluateAsync_ValidExpression_ReturnsExpectedResult(string expression, bool expected)
    {
        // Arrange
        var condition = new ExpressionCondition
        {
            Type = "expression",
            Expression = expression
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _sensorData);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("invalid expression")]
    [InlineData("temperature >")] // Incomplete expression
    [InlineData("temperature > abc")] // Invalid value
    public async Task EvaluateAsync_InvalidExpression_ThrowsArgumentException(string expression)
    {
        // Arrange
        var condition = new ExpressionCondition
        {
            Type = "expression",
            Expression = expression
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _evaluator.EvaluateAsync(condition, _sensorData));
    }

    [Fact]
    public async Task EvaluateAsync_ExpressionWithUnknownVariable_ThrowsArgumentException()
    {
        // Arrange
        var condition = new ExpressionCondition
        {
            Type = "expression",
            Expression = "unknown_sensor > 20"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => 
            _evaluator.EvaluateAsync(condition, _sensorData));
        
        Assert.Contains("Error evaluating expression", ex.Message);
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
            Value = 20.0
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _evaluator.EvaluateAsync(condition, _sensorData));
    }

    [Theory]
    [InlineData("temperature > 20 && Abs(humidity - 60) < 1")] // Using math functions
    [InlineData("Round(temperature) == 25")] // Rounding
    [InlineData("Floor(temperature) <= 25")] // Floor function
    [InlineData("Ceiling(temperature) >= 25")] // Ceiling function
    [InlineData("Max(temperature, humidity) == 60")] // Max function
    [InlineData("Min(temperature, humidity) == 25")] // Min function
    [InlineData("Sqrt(temperature * temperature) == temperature")] // Square root
    [InlineData("Pow(temperature, 2) > 600")] // Power function
    public async Task EvaluateAsync_MathFunctions_ReturnsTrue(string expression)
    {
        // Arrange
        var condition = new ExpressionCondition
        {
            Type = "expression",
            Expression = expression
        };

        // Act
        var result = await _evaluator.EvaluateAsync(condition, _sensorData);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task PerformanceTest_ExpressionEvaluation()
    {
        // Arrange
        var condition = new ExpressionCondition
        {
            Type = "expression",
            Expression = "(temperature > 20 && humidity < 70) || (pressure > 1000 && Abs(temperature - humidity) > 30)"
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
        Assert.True(averageTime < 0.1, $"Average evaluation time ({averageTime:F3}ms) exceeded threshold (0.1ms)");
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
            "Sqrt(pressure) > 30"
        };

        var conditions = expressions.Select(e => new ExpressionCondition
        {
            Type = "expression",
            Expression = e
        }).ToList();

        var iterations = 1000;
        var sw = Stopwatch.StartNew();

        // Act - First call (includes compilation)
        foreach (var condition in conditions)
        {
            await _evaluator.EvaluateAsync(condition, _sensorData);
        }
        var compilationTime = sw.ElapsedMilliseconds;
        _output.WriteLine($"First evaluation of {expressions.Length} expressions (including compilation): {compilationTime}ms");

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
        _output.WriteLine($"Total time for {iterations * expressions.Length} evaluations: {totalTime}ms");
        
        // Verify performance
        Assert.True(averageTime < 0.1, $"Average evaluation time ({averageTime:F3}ms) exceeded threshold (0.1ms)");
    }
}
