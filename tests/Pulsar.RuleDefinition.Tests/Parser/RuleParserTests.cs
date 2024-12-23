using System;
using System.IO;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Parser;
using Pulsar.RuleDefinition.Validation;
using Xunit;

namespace Pulsar.RuleDefinition.Tests.Parser;

public class RuleParserTests
{
  private readonly RuleParser _parser;
  private readonly RuleValidator _validator;
  private readonly SystemConfig _systemConfig;
  private readonly string _sampleRulesPath;
  private readonly string _systemConfigPath;

  /*************  ✨ Codeium Command ⭐  *************/
  /// <summary>
  /// Tests the <see cref="RuleParser"/> class. This class is responsible for parsing
  /// rule definition YAML files into strongly-typed objects.
  /// </summary>
  /******  4d93c6a5-879e-4908-85d5-76d36ccf5e93  *******/
  public RuleParserTests()
  {
    _parser = new RuleParser();
    _systemConfigPath = Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "system_config.yaml"
    );
    _sampleRulesPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample_rules.yaml");

    var configParser = new SystemConfigParser();
    _systemConfig = configParser.ParseFile(_systemConfigPath);
    _validator = new RuleValidator(_systemConfig);
  }

  [Fact]
  public void ParseRules_ValidYaml_SuccessfullyParses()
  {
    // Act
    var ruleSet = _parser.ParseRulesFromFile(_sampleRulesPath);

    // Assert
    Assert.NotNull(ruleSet);
    Assert.Equal(1, ruleSet.Version);
    Assert.NotEmpty(ruleSet.Rules);
  }

  [Fact]
  public void ParseRules_ValidYaml_ContainsExpectedRules()
  {
    // Act
    var ruleSet = _parser.ParseRulesFromFile(_sampleRulesPath);

    // Assert
    Assert.Contains(ruleSet.Rules, r => r.Name == "HighTemperatureAlert");
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
    Assert.Empty(result.Errors);
  }

  [Fact]
  public void ParseRules_InvalidYaml_ThrowsException()
  {
    // Arrange
    var invalidYaml =
        @"
version: 1
rules:
  - name: Test
    conditions:
      all:
        - type: comparison  # Invalid YAML structure
          data_source: temperature
          operator: '>
          value: 50"; // Missing closing quote

    // Act & Assert
    Assert.Throws<ArgumentException>(() => _parser.ParseRules(invalidYaml));
  }

  [Fact]
  public void ParseRules_FileNotFound_ThrowsException()
  {
    // Act & Assert
    Assert.Throws<FileNotFoundException>(() => _parser.ParseRulesFromFile("nonexistent.yaml"));
  }

  [Fact]
  public void ParseRules_EmptyFile_ThrowsException()
  {
    // Arrange
    var emptyYaml = string.Empty;

    // Act & Assert
    Assert.Throws<ArgumentException>(() => _parser.ParseRules(emptyYaml));
  }

  [Fact]
  public void ParseRules_MissingName_ThrowsException()
  {
    // Arrange
    var yaml =
        @"
version: 1
rules:
  - conditions:
      all:
        - type: comparison
          data_source: temperature
          operator: '>'
          value: 50";

    // Act & Assert
    Assert.Throws<ArgumentException>(() => _parser.ParseRules(yaml));
  }

  [Fact]
  public void ParseRules_MissingConditions_ThrowsException()
  {
    // Arrange
    var yaml =
        @"
version: 1
rules:
  - name: Test
    actions:
      - type: notify
        message: Test message";

    // Act & Assert
    Assert.Throws<ArgumentException>(() => _parser.ParseRules(yaml));
  }

  [Fact]
  public void ParseRules_InvalidConditionType_ThrowsException()
  {
    // Arrange
    var yaml =
        @"
version: 1
rules:
  - name: Test
    conditions:
      all:
        - type: invalid_type
          data_source: temperature
          operator: '>'
          value: 50";

    // Act & Assert
    Assert.Throws<ArgumentException>(() => _parser.ParseRules(yaml));
  }
}
