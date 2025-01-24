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
    public string Threshold { get; set; } = string.Empty;

    /// <summary>
    /// Duration that the condition must be met (e.g. "500ms", "1s", "5m")
    /// </summary>
    public string Duration { get; set; } = string.Empty;

    /// <summary>
    /// The comparison operator (">", "<", ">=", "<=", "==", "!=")
    /// </summary>
    public string Operator { get; set; } = ">";
}
