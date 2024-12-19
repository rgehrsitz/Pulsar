using System.Text.RegularExpressions;

namespace Pulsar.RuleDefinition.Validation;

/// <summary>
/// Validates temporal conditions and durations
/// </summary>
public class TemporalValidator
{
    private const int MaxDurationMs = 60000; // 1 minute maximum duration
    private const int MinDurationMs = 100;   // 100ms minimum duration

    /// <summary>
    /// Validates a duration string and converts it to milliseconds
    /// </summary>
    public (bool IsValid, int Milliseconds, string? Error) ValidateDuration(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return (false, 0, "Duration cannot be empty");
        }

        // Parse duration in milliseconds
        var match = Regex.Match(duration, @"^(\d+)ms$");
        if (!match.Success)
        {
            return (false, 0, "Duration must be specified in milliseconds (e.g., '500ms')");
        }

        if (!int.TryParse(match.Groups[1].Value, out int ms))
        {
            return (false, 0, "Invalid duration value");
        }

        // Validate duration range
        if (ms < MinDurationMs)
        {
            return (false, 0, $"Duration must be at least {MinDurationMs}ms");
        }

        if (ms > MaxDurationMs)
        {
            return (false, 0, $"Duration cannot exceed {MaxDurationMs}ms");
        }

        return (true, ms, null);
    }

    /// <summary>
    /// Validates that temporal conditions are properly configured
    /// </summary>
    public List<string> ValidateTemporalCondition(Models.ThresholdOverTimeCondition condition)
    {
        var errors = new List<string>();

        // Validate duration format and value
        var (isValid, _, error) = ValidateDuration(condition.Duration);
        if (!isValid)
        {
            errors.Add(error!);
        }

        // Validate threshold value
        if (double.IsNaN(condition.Threshold) || double.IsInfinity(condition.Threshold))
        {
            errors.Add("Invalid threshold value");
        }

        return errors;
    }
}
