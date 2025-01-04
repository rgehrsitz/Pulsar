using System;

namespace Pulsar.RuleDefinition.Models.Conditions;

/// <summary>
/// Represents a condition that checks if a sensor value stays above/below a threshold for a specified duration
/// </summary>
public class ThresholdOverTimeConditionDefinition : Condition
{
    /// <summary>
    /// The sensor to monitor
    /// </summary>
    public string DataSource { get; set; } = string.Empty;

    /// <summary>
    /// The threshold value to compare against
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// Duration in milliseconds that the condition must be met
    /// </summary>
    public TimeSpan Duration { get; set; }
}
