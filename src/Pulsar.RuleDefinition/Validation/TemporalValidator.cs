using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Pulsar.RuleDefinition.Models.Conditions;

namespace Pulsar.RuleDefinition.Validation;

/// <summary>
/// Validates temporal conditions and durations
/// </summary>
public class TemporalValidator
{
    private const int MinDurationMs = 100; // Minimum 100ms
    private const int MaxDurationMs = 86400000; // Maximum 24 hours
    private const int MaxLookbackPeriods = 1000; // Maximum number of historical values to store

    private static readonly Dictionary<string, int> DurationMultipliers = new()
    {
        { "ms", 1 },
        { "s", 1000 },
        { "m", 60000 },
        { "h", 3600000 },
        { "d", 86400000 },
    };

    /// <summary>
    /// Validates that temporal conditions are properly configured
    /// </summary>
    public List<string> ValidateTemporalCondition(ThresholdOverTimeConditionDefinition condition)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(condition.DataSource))
        {
            errors.Add("Data source must be specified");
        }

        if (!double.TryParse(condition.Threshold, out var threshold) || double.IsNaN(threshold))
        {
            errors.Add("Invalid threshold value");
        }

        var (isValid, durationMs, error) = ValidateDuration(condition.Duration);
        if (!isValid)
        {
            errors.Add(error);
        }
        else if (durationMs < MinDurationMs)
        {
            errors.Add($"Duration must be at least {MinDurationMs}ms");
        }
        else if (durationMs > MaxDurationMs)
        {
            errors.Add($"Duration cannot exceed {MaxDurationMs}ms (24 hours)");
        }

        return errors;
    }

    /// <summary>
    /// Validates a duration string and converts it to milliseconds
    /// </summary>
    /// <param name="duration">Duration string (e.g. "500ms", "1s", "5m")</param>
    /// <returns>Tuple of (isValid, milliseconds, error)</returns>
    public (bool IsValid, int Milliseconds, string Error) ValidateDuration(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return (false, 0, "Duration must be specified");
        }

        var match = Regex.Match(duration, @"^(\d+)(ms|s|m|h|d)$");
        if (!match.Success)
        {
            return (false, 0, "Invalid duration format. Must be a number followed by a unit (ms, s, m, h, d)");
        }

        if (!int.TryParse(match.Groups[1].Value, out var value))
        {
            return (false, 0, "Invalid duration value");
        }

        if (value <= 0)
        {
            return (false, 0, "Duration must be positive");
        }

        var unit = match.Groups[2].Value;
        var multiplier = DurationMultipliers[unit];
        var milliseconds = value * multiplier;

        if (milliseconds > MaxDurationMs)
        {
            return (false, 0, $"Duration cannot exceed {MaxDurationMs}ms (24 hours)");
        }

        return (true, milliseconds, string.Empty);
    }

    /// <summary>
    /// Calculates the number of data points required for a given duration and sampling interval
    /// </summary>
    public (bool IsValid, int DataPoints, string Error) CalculateRequiredDataPoints(string duration, TimeSpan samplingInterval)
    {
        var (isValid, durationMs, error) = ValidateDuration(duration);
        if (!isValid)
        {
            return (false, 0, error);
        }

        var dataPoints = (int)Math.Ceiling(durationMs / samplingInterval.TotalMilliseconds);

        if (dataPoints > MaxLookbackPeriods)
        {
            return (false, 0, $"Duration and sampling interval would require too many data points ({dataPoints} > {MaxLookbackPeriods})");
        }

        return (true, dataPoints, string.Empty);
    }
}
