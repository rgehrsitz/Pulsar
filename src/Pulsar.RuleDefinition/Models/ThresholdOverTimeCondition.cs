using System;

namespace Pulsar.RuleDefinition.Models;

/// <summary>
/// Represents a condition that evaluates a threshold over a time window
/// </summary>
public class ThresholdOverTimeCondition : Condition
{
    /// <summary>
    /// The data source to monitor
    /// </summary>
    public string DataSource { get; set; } = "";

    /// <summary>
    /// The threshold value to compare against
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// The duration to evaluate over (e.g. "500ms", "5s", "1m")
    /// </summary>
    public string Duration { get; set; } = "";

    /// <summary>
    /// The threshold operator to use (e.g. GreaterThan, LessThan)
    /// </summary>
    public ThresholdOperator Operator { get; set; }

    /// <summary>
    /// The minimum percentage of values that must meet the threshold condition (0.0 to 1.0)
    /// If not specified, defaults to 1.0 (100%)
    /// </summary>
    public double RequiredPercentage { get; set; } = 1.0;

    /// <summary>
    /// Gets the duration in milliseconds
    /// </summary>
    public long DurationMs
    {
        get
        {
            if (string.IsNullOrEmpty(Duration))
            {
                return 0;
            }

            // Find the last digit to separate number from unit
            int i = Duration.Length - 1;
            while (i >= 0 && !char.IsDigit(Duration[i]))
            {
                i--;
            }

            if (i < 0)
            {
                throw new ArgumentException($"Invalid duration format: {Duration}");
            }

            var value = double.Parse(Duration[..(i + 1)]);
            var unit = Duration[(i + 1)..];

            return unit switch
            {
                "ms" => (long)value,
                "s" => (long)(value * 1000),
                "m" => (long)(value * 60 * 1000),
                "h" => (long)(value * 60 * 60 * 1000),
                _ => throw new ArgumentException($"Invalid duration unit: {unit}"),
            };
        }
    }
}
