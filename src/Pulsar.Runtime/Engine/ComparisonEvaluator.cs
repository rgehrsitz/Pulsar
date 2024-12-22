using Pulsar.RuleDefinition.Models;
using Serilog;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Evaluates comparison conditions against sensor data
/// </summary>
public class ComparisonEvaluator : IConditionEvaluator
{
    private static readonly Dictionary<string, Func<double, double, bool>> Operators = new()
    {
        [">"] = (a, b) => a > b,
        ["<"] = (a, b) => a < b,
        [">="] = (a, b) => a >= b,
        ["<="] = (a, b) => a <= b,
        ["=="] = (a, b) => Math.Abs(a - b) < double.Epsilon,
        ["!="] = (a, b) => Math.Abs(a - b) >= double.Epsilon,
    };

    private readonly ILogger _logger;

    public ComparisonEvaluator(ILogger? logger = null)
    {
        _logger = (logger ?? Log.Logger).ForContext<ComparisonEvaluator>();
    }

    public Task<bool> EvaluateAsync(Condition condition, IDictionary<string, double> sensorData)
    {
        if (condition is not ComparisonCondition comparisonCondition)
        {
            _logger.Error(
                "Invalid condition type. Expected {ExpectedType} but got {ActualType}",
                typeof(ComparisonCondition).Name,
                condition.GetType().Name
            );
            throw new ArgumentException(
                $"Expected ComparisonCondition but got {condition.GetType().Name}"
            );
        }

        _logger.Debug(
            "Evaluating comparison condition for {DataSource} {Operator} {Value}",
            comparisonCondition.DataSource,
            comparisonCondition.Operator,
            comparisonCondition.Value
        );

        if (!sensorData.TryGetValue(comparisonCondition.DataSource, out double currentValue))
        {
            _logger.Warning(
                "Data source {DataSource} not found in sensor data. Available sources: {@Sources}",
                comparisonCondition.DataSource,
                sensorData.Keys
            );
            throw new KeyNotFoundException(
                $"Data source '{comparisonCondition.DataSource}' not found in sensor data"
            );
        }

        if (!Operators.TryGetValue(comparisonCondition.Operator, out var operation))
        {
            _logger.Error(
                "Invalid operator {Operator}. Valid operators: {@ValidOperators}",
                comparisonCondition.Operator,
                Operators.Keys
            );
            throw new ArgumentException($"Invalid operator: {comparisonCondition.Operator}");
        }

        var result = operation(currentValue, comparisonCondition.Value);
        _logger.Debug(
            "Comparison result for {DataSource}: {CurrentValue} {Operator} {CompareValue} = {Result}",
            comparisonCondition.DataSource,
            currentValue,
            comparisonCondition.Operator,
            comparisonCondition.Value,
            result
        );

        return Task.FromResult(result);
    }
}
