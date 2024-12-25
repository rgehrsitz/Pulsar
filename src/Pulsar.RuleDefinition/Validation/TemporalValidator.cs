using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
    public List<string> ValidateTemporalCondition(Models.ThresholdOverTimeCondition condition)
    {
        var errors = new List<string>();

        // Validate duration value
        if (condition.DurationMs < MinDurationMs)
        {
            errors.Add($"Duration must be at least {MinDurationMs}ms");
        }
        else if (condition.DurationMs > MaxDurationMs)
        {
            errors.Add($"Duration cannot exceed 24 hours ({MaxDurationMs}ms)");
        }

        // Validate threshold value
        if (double.IsNaN(condition.Threshold) || double.IsInfinity(condition.Threshold))
        {
            errors.Add("Invalid threshold value");
        }

        // Validate data source
        if (string.IsNullOrWhiteSpace(condition.DataSource))
        {
            errors.Add("Data source must be specified");
        }

        return errors;
    }

    /// <summary>
    /// Validates a duration string and converts it to milliseconds
    /// </summary>
    public (bool IsValid, int Milliseconds, string? Error) ValidateDuration(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return (false, 0, "Duration cannot be empty");
        }

        // Parse duration with unit
        var match = Regex.Match(duration, @"^(\d+)(ms|s|m|h|d)$");
        if (!match.Success)
        {
            return (false, 0, "Duration must be specified with a valid unit (ms, s, m, h, d)");
        }

        if (!int.TryParse(match.Groups[1].Value, out int value))
        {
            return (false, 0, "Invalid duration value");
        }

        var unit = match.Groups[2].Value;
        var multiplier = DurationMultipliers[unit];
        var totalMs = value * multiplier;

        // Validate duration range
        if (totalMs < MinDurationMs)
        {
            return (false, 0, $"Duration must be at least {MinDurationMs}ms");
        }

        if (totalMs > MaxDurationMs)
        {
            return (false, 0, $"Duration cannot exceed {MaxDurationMs}ms");
        }

        return (true, totalMs, null);
    }

    /// <summary>
    /// Calculates the number of data points needed for a given duration and sampling rate
    /// </summary>
    public (bool IsValid, int DataPoints, string? Error) CalculateRequiredDataPoints(
        string duration,
        TimeSpan samplingRate
    )
    {
        var (isValid, durationMs, error) = ValidateDuration(duration);
        if (!isValid)
        {
            return (false, 0, error);
        }

        var dataPoints = (int)(durationMs / samplingRate.TotalMilliseconds);
        if (dataPoints > MaxLookbackPeriods)
        {
            return (
                false,
                0,
                $"Duration requires too many data points ({dataPoints}). Maximum is {MaxLookbackPeriods}"
            );
        }

        return (true, dataPoints, null);
    }
}
