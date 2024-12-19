using Pulsar.RuleDefinition.Validation;
using Xunit;

namespace Pulsar.RuleDefinition.Tests.Validation;

public class ExpressionValidatorTests
{
    private readonly ExpressionValidator _validator;

    public ExpressionValidatorTests()
    {
        _validator = new ExpressionValidator();
    }

    [Theory]
    [InlineData("temperature > 50", true)]
    [InlineData("(humidity * 1.8 + 32) > 100", true)]
    [InlineData("min(temperature, 50) > 25", true)]
    [InlineData("sqrt(pressure) < 100", true)]
    [InlineData("", false)]
    [InlineData(">>", false)]
    [InlineData("temperature >", false)]
    [InlineData("temperature > > 50", false)]
    [InlineData("invalid_function(temperature)", false)]
    public void ValidateExpression_BasicSyntax(string expression, bool expectedValid)
    {
        var (isValid, _, _) = _validator.ValidateExpression(expression);
        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData("temperature > 50", new[] { "temperature" })]
    [InlineData("humidity + pressure > 100", new[] { "humidity", "pressure" })]
    [InlineData("min(temp1, temp2)", new[] { "temp1", "temp2" })]
    [InlineData("sqrt(100) > 0", new string[] { })]
    public void ValidateExpression_ExtractsDataSources(string expression, string[] expectedSources)
    {
        var (_, dataSources, _) = _validator.ValidateExpression(expression);
        Assert.Equal(expectedSources.OrderBy(x => x), dataSources.OrderBy(x => x));
    }

    [Theory]
    [InlineData("temperature > 50", true)]
    [InlineData("temperature + humidity", false)]  // Missing comparison
    [InlineData("temperature > > 50", false)]      // Invalid operator sequence
    [InlineData("* temperature > 50", false)]      // Invalid start
    [InlineData("temperature > ", false)]          // Missing right operand
    public void ValidateExpression_OperatorValidation(string expression, bool expectedValid)
    {
        var (isValid, _, _) = _validator.ValidateExpression(expression);
        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData("min(temperature, 50)", true)]
    [InlineData("max(humidity, pressure)", true)]
    [InlineData("sqrt(pressure)", true)]
    [InlineData("unknown(temperature)", false)]
    [InlineData("min(", false)]
    [InlineData("min)", false)]
    [InlineData("min(,)", false)]
    public void ValidateExpression_FunctionValidation(string expression, bool expectedValid)
    {
        var (isValid, _, _) = _validator.ValidateExpression(expression);
        Assert.Equal(expectedValid, isValid);
    }
}
