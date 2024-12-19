using Pulsar.RuleDefinition.Validation;
using Pulsar.RuleDefinition.Models;
using Xunit;

namespace Pulsar.RuleDefinition.Tests.Validation;

public class TemporalValidatorTests
{
    private readonly TemporalValidator _validator;

    public TemporalValidatorTests()
    {
        _validator = new TemporalValidator();
    }

    [Theory]
    [InlineData("500ms", true)]
    [InlineData("1000ms", true)]
    [InlineData("60000ms", true)]
    [InlineData("", false)]
    [InlineData("500", false)]
    [InlineData("500s", false)]
    [InlineData("99ms", false)]      // Too short
    [InlineData("60001ms", false)]   // Too long
    public void ValidateDuration_Format(string duration, bool expectedValid)
    {
        var (isValid, _, _) = _validator.ValidateDuration(duration);
        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData("500ms", 500)]
    [InlineData("1000ms", 1000)]
    [InlineData("60000ms", 60000)]
    public void ValidateDuration_Conversion(string duration, int expectedMs)
    {
        var (isValid, milliseconds, _) = _validator.ValidateDuration(duration);
        Assert.True(isValid);
        Assert.Equal(expectedMs, milliseconds);
    }

    [Fact]
    public void ValidateTemporalCondition_ValidCondition()
    {
        var condition = new ThresholdOverTimeCondition
        {
            DataSource = "temperature",
            Threshold = 50,
            Duration = "500ms"
        };

        var errors = _validator.ValidateTemporalCondition(condition);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateTemporalCondition_InvalidDuration()
    {
        var condition = new ThresholdOverTimeCondition
        {
            DataSource = "temperature",
            Threshold = 50,
            Duration = "invalid"
        };

        var errors = _validator.ValidateTemporalCondition(condition);
        Assert.Single(errors);
        Assert.Contains("Duration must be specified in milliseconds", errors[0]);
    }

    [Fact]
    public void ValidateTemporalCondition_InvalidThreshold()
    {
        var condition = new ThresholdOverTimeCondition
        {
            DataSource = "temperature",
            Threshold = double.NaN,
            Duration = "500ms"
        };

        var errors = _validator.ValidateTemporalCondition(condition);
        Assert.Single(errors);
        Assert.Contains("Invalid threshold value", errors[0]);
    }
}
