using Pulsar.RuleDefinition.Parser;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Validation;

namespace Pulsar.RuleDefinition.Tests.Parser;

public class RuleParserTests
{
    private readonly RuleParser _parser;
    private readonly RuleValidator _validator;
    private readonly string _sampleRulesPath;

    public RuleParserTests()
    {
        _parser = new RuleParser();
        _validator = new RuleValidator();
        _sampleRulesPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample_rules.yaml");
    }

    [Fact]
    public void ParseRules_ValidYaml_SuccessfullyParses()
    {
        // Act
        var ruleSet = _parser.ParseRulesFromFile(_sampleRulesPath);

        // Assert
        Assert.NotNull(ruleSet);
        Assert.Equal(1, ruleSet.Version);
        Assert.Equal(8, ruleSet.ValidDataSources.Count);
        Assert.Equal(4, ruleSet.Rules.Count);
    }

    [Fact]
    public void ParseRules_ValidYaml_ContainsExpectedRules()
    {
        // Act
        var ruleSet = _parser.ParseRulesFromFile(_sampleRulesPath);

        // Assert
        Assert.Contains(ruleSet.Rules, r => r.Name == "HighTemperatureAlert");
        Assert.Contains(ruleSet.Rules, r => r.Name == "CombinedEnvironmentAlert");
        Assert.Contains(ruleSet.Rules, r => r.Name == "TemperatureConversion");
        Assert.Contains(ruleSet.Rules, r => r.Name == "ComplexCondition");
    }

    [Fact]
    public void ValidateRules_ValidYaml_NoValidationErrors()
    {
        // Arrange
        var ruleSet = _parser.ParseRulesFromFile(_sampleRulesPath);

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ParseRules_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(AppContext.BaseDirectory, "nonexistent.yaml");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _parser.ParseRulesFromFile(nonExistentPath));
    }

    [Fact]
    public void ParseRules_InvalidYaml_ThrowsRuleParsingException()
    {
        // Arrange
        const string invalidYaml = @"
            version: 1
            valid_data_sources: [
                - invalid
                yaml
                content
        ";

        // Act & Assert
        Assert.Throws<RuleParsingException>(() => _parser.ParseRules(invalidYaml));
    }

    [Theory]
    [InlineData("HighTemperatureAlert", "threshold_over_time")]
    [InlineData("CombinedEnvironmentAlert", "comparison")]
    [InlineData("TemperatureConversion", "expression")]
    public void ParseRules_ConditionTypes_AreCorrectlyParsed(string ruleName, string expectedType)
    {
        // Arrange
        var ruleSet = _parser.ParseRulesFromFile(_sampleRulesPath);
        var rule = ruleSet.Rules.First(r => r.Name == ruleName);

        // Act & Assert
        var condition = rule.Conditions.All?.FirstOrDefault()?.Type 
            ?? rule.Conditions.Any?.FirstOrDefault()?.Type;
        
        Assert.Equal(expectedType, condition);
    }
}
