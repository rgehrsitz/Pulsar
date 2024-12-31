using System;
using Pulsar.RuleDefinition.Validation;
using Pulsar.RuleDefinition.Models;
using Xunit;

namespace Pulsar.RuleDefinition.Tests.Validation;

public class TemporalValidatorTests
{
    private readonly TemporalValidator _validator = new();

    [Theory]
    [InlineData("500ms", true)]  // 500 milliseconds
    [InlineData("1s", true)]     // 1 second
    [InlineData("5m", true)]     // 5 minutes
    [InlineData("1h", true)]     // 1 hour
    [InlineData("1d", true)]     // 1 day
    [InlineData("", false)]      // Empty
    [InlineData("invalid", false)]  // Invalid format
    [InlineData("5x", false)]    // Invalid unit
    [InlineData("-5m", false)]   // Negative duration
    [InlineData("0ms", false)]   // Zero duration
    [InlineData("25h", false)]   // Exceeds maximum (24h)
    public void ValidateDuration_ReturnsExpectedResult(string duration, bool expectedValid)
    {
        var (isValid, _, _) = _validator.ValidateDuration(duration);
        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData("500ms", 500)]    // 500 milliseconds
    [InlineData("1s", 1000)]      // 1 second = 1000ms
    [InlineData("1m", 60000)]     // 1 minute = 60000ms
    [InlineData("1h", 3600000)]   // 1 hour = 3600000ms
    public void ValidateDuration_ReturnsCorrectMilliseconds(string duration, int expectedMs)
    {
        var (isValid, milliseconds, _) = _validator.ValidateDuration(duration);
        Assert.True(isValid);
        Assert.Equal(expectedMs, milliseconds);
    }

    [Fact]
    public void ValidateTemporalCondition_ValidCondition_NoErrors()
    {
        var condition = new ThresholdOverTimeCondition
        {
            DataSource = "temperature",
            Threshold = 50,
            DurationMs = 500
        };

        var errors = _validator.ValidateTemporalCondition(condition);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateTemporalCondition_InvalidThreshold_ReturnsError()
    {
        var condition = new ThresholdOverTimeCondition
        {
            DataSource = "temperature",
            Threshold = double.NaN,
            DurationMs = 500
        };

        var errors = _validator.ValidateTemporalCondition(condition);
        Assert.Single(errors);
        Assert.Contains("Invalid threshold value", errors[0]);
    }

    [Fact]
    public void ValidateTemporalCondition_InvalidDuration_ReturnsError()
    {
        var condition = new ThresholdOverTimeCondition
        {
            DataSource = "temperature",
            Threshold = 50,
            DurationMs = -1
        };

        var errors = _validator.ValidateTemporalCondition(condition);
        Assert.Single(errors);
        Assert.Contains("Duration must be at least 100ms", errors[0]);
    }

    [Fact]
    public void ValidateTemporalCondition_EmptyDataSource_ReturnsError()
    {
        var condition = new ThresholdOverTimeCondition
        {
            DataSource = "",
            Threshold = 50,
            DurationMs = 500
        };

        var errors = _validator.ValidateTemporalCondition(condition);
        Assert.Single(errors);
        Assert.Contains("Data source must be specified", errors[0]);
    }

    [Theory]
    [InlineData("1s", 100, 10)]    // 1 second at 100ms sampling = 10 points
    [InlineData("1m", 1000, 60)]   // 1 minute at 1s sampling = 60 points
    [InlineData("1h", 60000, 60)]  // 1 hour at 1m sampling = 60 points
    public void CalculateRequiredDataPoints_ReturnsExpectedPoints(string duration, int samplingMs, int expectedPoints)
    {
        var (isValid, dataPoints, _) = _validator.CalculateRequiredDataPoints(
            duration,
            TimeSpan.FromMilliseconds(samplingMs));

        Assert.True(isValid);
        Assert.Equal(expectedPoints, dataPoints);
    }

    [Fact]
    public void CalculateRequiredDataPoints_TooManyPoints_ReturnsError()
    {
        // Request 1 day of data at 1ms sampling = 86,400,000 points (exceeds max)
        var (isValid, _, error) = _validator.CalculateRequiredDataPoints(
            "1d",
            TimeSpan.FromMilliseconds(1));

        Assert.False(isValid);
        Assert.Contains("too many data points", error);
    }
}
