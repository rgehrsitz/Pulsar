using Pulsar.RuleDefinition.Models;

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
        ["!="] = (a, b) => Math.Abs(a - b) >= double.Epsilon
    };

    public Task<bool> EvaluateAsync(Condition condition, IDictionary<string, double> sensorData)
    {
        if (condition is not ComparisonCondition comparisonCondition)
        {
            throw new ArgumentException($"Expected ComparisonCondition but got {condition.GetType().Name}");
        }

        if (!sensorData.TryGetValue(comparisonCondition.DataSource, out double currentValue))
        {
            throw new KeyNotFoundException($"Data source '{comparisonCondition.DataSource}' not found in sensor data");
        }

        if (!Operators.TryGetValue(comparisonCondition.Operator, out var operation))
        {
            throw new ArgumentException($"Invalid operator: {comparisonCondition.Operator}");
        }

        return Task.FromResult(operation(currentValue, comparisonCondition.Value));
    }
}
