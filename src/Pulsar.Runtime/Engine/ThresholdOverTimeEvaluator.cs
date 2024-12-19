using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.RuleDefinition.Models;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Evaluates threshold over time conditions against sensor data
/// </summary>
public class ThresholdOverTimeEvaluator : IConditionEvaluator
{
    private readonly ISensorDataProvider _dataProvider;

    public ThresholdOverTimeEvaluator(ISensorDataProvider dataProvider)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
    }

    public async Task<bool> EvaluateAsync(Condition condition, IDictionary<string, double> sensorData)
    {
        if (condition is not ThresholdOverTimeCondition thresholdCondition)
        {
            throw new ArgumentException($"Expected ThresholdOverTimeCondition but got {condition.GetType().Name}");
        }

        if (!sensorData.ContainsKey(thresholdCondition.DataSource))
        {
            return false;
        }

        var duration = ParseDuration(thresholdCondition.Duration);
        var historicalData = await _dataProvider.GetHistoricalDataAsync(thresholdCondition.DataSource, duration);

        if (!historicalData.Any())
            return false;

        var average = historicalData.Average(d => d.Value);
        return average > thresholdCondition.Threshold;
    }

    private static TimeSpan ParseDuration(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            throw new ArgumentException("Duration cannot be empty");

        if (duration.Length < 2)
            throw new ArgumentException("Duration must be in format: [number][unit] (e.g., '5m', '1h')");

        if (!int.TryParse(duration[..^1], out var value) || value <= 0)
            throw new ArgumentException($"Invalid duration format: {duration}. Expected format: [positive number][unit] (e.g., '5m', '1h')");

        var unit = duration[^1];

        return unit switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            'd' => TimeSpan.FromDays(value),
            _ => throw new ArgumentException($"Invalid duration unit: {unit}. Valid units are: s (seconds), m (minutes), h (hours), d (days)")
        };
    }
}
