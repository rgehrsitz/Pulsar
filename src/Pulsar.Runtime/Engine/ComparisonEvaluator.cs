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
        if (condition?.Condition is not ComparisonConditionDefinition comparison)
        {
            _logger.Error(
                "Invalid condition type. Expected {ExpectedType} but got {ActualType}",
                typeof(ComparisonConditionDefinition).Name,
                condition?.GetType().Name ?? "null"
            );
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(comparison.DataSource))
        {
            _logger.Error("Data source is required");
            return Task.FromResult(false);
        }

        if (!sensorData.TryGetValue(comparison.DataSource, out var sensorValue))
        {
            _logger.Error("Data source not found: {DataSource}", comparison.DataSource);
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(comparison.Value))
        {
            _logger.Error("Value is required");
            return Task.FromResult(false);
        }

        if (!double.TryParse(comparison.Value, out var value))
        {
            _logger.Error("Invalid value: {Value}", comparison.Value);
            return Task.FromResult(false);
        }

        var result = comparison.Operator switch
        {
            ">" => sensorValue > value,
            "<" => sensorValue < value,
            ">=" => sensorValue >= value,
            "<=" => sensorValue <= value,
            "==" => Math.Abs(sensorValue - value) < 0.0001, // Use epsilon for floating-point comparison
            "!=" => Math.Abs(sensorValue - value) >= 0.0001,
            _ => false
        };

        _logger.Debug(
            "Evaluated condition: {DataSource} {Operator} {Value} = {Result}",
            comparison.DataSource,
            comparison.Operator,
            comparison.Value,
            result
        );

        return Task.FromResult(result);
    }
}
