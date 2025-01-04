using System;
using System.Collections.Generic;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Conditions;
using Serilog;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Evaluates comparison conditions against sensor data
/// </summary>
public class ComparisonEvaluator : IConditionEvaluator
{
    private readonly ILogger _logger;

    public ComparisonEvaluator(ILogger logger)
    {
        _logger = logger.ForContext<ComparisonEvaluator>();
    }

    public Task<bool> EvaluateAsync(
        ConditionDefinition condition,
        IDictionary<string, double> sensorData
    )
    {
        if (condition is not ComparisonConditionDefinition comparisonCondition)
        {
            _logger.Error(
                "Invalid condition type. Expected {ExpectedType} but got {ActualType}",
                typeof(ComparisonConditionDefinition).Name,
                condition.GetType().Name
            );
            throw new ArgumentException(
                $"Expected ComparisonConditionDefinition but got {condition.GetType().Name}"
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

        var result = comparisonCondition.Operator switch
        {
            ThresholdOperator.GreaterThan => currentValue > comparisonCondition.Value,
            ThresholdOperator.GreaterThanOrEqual => currentValue >= comparisonCondition.Value,
            ThresholdOperator.LessThan => currentValue < comparisonCondition.Value,
            ThresholdOperator.LessThanOrEqual => currentValue <= comparisonCondition.Value,
            ThresholdOperator.Equal => Math.Abs(currentValue - comparisonCondition.Value) < double.Epsilon,
            ThresholdOperator.NotEqual => Math.Abs(currentValue - comparisonCondition.Value) >= double.Epsilon,
            _ => throw new ArgumentException($"Unknown operator: {comparisonCondition.Operator}")
        };

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
