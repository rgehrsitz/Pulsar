using System.Collections.Generic;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Actions;
using Pulsar.RuleDefinition.Models.Conditions;
using Pulsar.RuleDefinition.Validation;
using Xunit;

namespace Pulsar.RuleDefinition.Tests.Validation;

public class RuleValidatorTests
{
    private readonly RuleValidator _validator;
    private readonly SystemConfig _systemConfig;

    public RuleValidatorTests()
    {
        _systemConfig = new SystemConfig
        {
            Version = 1,
            ValidSensors = new List<string>
            {
                "temperature",
                "humidity",
                "pressure",
                "alerts:temperature",
                "converted_temp",
                "derived_temp"
            }
        };
        _validator = new RuleValidator(_systemConfig);
    }

    [Fact]
    public void ValidateRuleSet_ValidRules_NoErrors()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<Rule>
            {
                new()
                {
                    Name = "Rule1",
                    Description = "Test rule",
                    Conditions = new ConditionGroup
                    {
                        All = new List<ConditionWrapper>
                        {
                            new ConditionWrapper
                            {
                                Condition = new ComparisonCondition
                                {
                                    Type = "comparison",
                                    DataSource = "temperature",
                                    Operator = ">",
                                    Value = 50
                                }
                            }
                        }
                    },
                    Actions = new List<RuleAction>
                    {
                        new RuleAction { SetValue = new SetValueAction { Key = "alerts:temperature", Value = "1" } }
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateRuleSet_InvalidVersion_ReturnsError()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 0,
            Rules = new List<Rule>()
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.Contains(result.Errors, e => e.Message.Contains("Version must be greater than 0"));
    }

    [Fact]
    public void ValidateRuleSet_EmptyRules_ReturnsError()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<Rule>()
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.Contains(result.Errors, e => e.Message.Contains("Rules list cannot be empty"));
    }

    [Fact]
    public void ValidateRuleSet_DuplicateRuleNames_ReturnsError()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<Rule>
            {
                CreateValidRule("Rule1"),
                CreateValidRule("Rule1")
            }
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate rule name"));
    }

    [Fact]
    public void ValidateRule_InvalidDataSource_ReturnsError()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<Rule>
            {
                new()
                {
                    Name = "TestRule",
                    Conditions = new ConditionGroup
                    {
                        All = new List<ConditionWrapper>
                        {
                            new ConditionWrapper
                            {
                                Condition = new ComparisonCondition
                                {
                                    Type = "comparison",
                                    DataSource = "invalid_sensor",
                                    Operator = ">",
                                    Value = 50
                                }
                            }
                        }
                    },
                    Actions = new List<RuleAction>
                    {
                        new RuleAction { SetValue = new SetValueAction { Key = "alerts:temperature", Value = "1" } }
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.Contains(result.Errors, e => e.Message.Contains("Invalid data source"));
    }

    [Fact]
    public void ValidateRule_InvalidOperator_ReturnsError()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<Rule>
            {
                new()
                {
                    Name = "TestRule",
                    Conditions = new ConditionGroup
                    {
                        All = new List<ConditionWrapper>
                        {
                            new ConditionWrapper
                            {
                                Condition = new ComparisonCondition
                                {
                                    Type = "comparison",
                                    DataSource = "temperature",
                                    Operator = "invalid",
                                    Value = 50
                                }
                            }
                        }
                    },
                    Actions = new List<RuleAction>
                    {
                        new RuleAction { SetValue = new SetValueAction { Key = "alerts:temperature", Value = "1" } }
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.Contains(result.Errors, e => e.Message.Contains("Invalid operator"));
    }

    [Fact]
    public void ValidateRule_NoConditions_ReturnsError()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<Rule>
            {
                new()
                {
                    Name = "TestRule",
                    Conditions = new ConditionGroup(),
                    Actions = new List<RuleAction>
                    {
                        new RuleAction { SetValue = new SetValueAction { Key = "alerts:temperature", Value = "1" } }
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.Contains(result.Errors, e => e.Message.Contains("Must have at least one condition"));
    }

    [Fact]
    public void ValidateRule_NoActions_ReturnsError()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<Rule>
            {
                new()
                {
                    Name = "TestRule",
                    Conditions = new ConditionGroup
                    {
                        All = new List<ConditionWrapper>
                        {
                            new ConditionWrapper
                            {
                                Condition = new ComparisonCondition
                                {
                                    Type = "comparison",
                                    DataSource = "temperature",
                                    Operator = ">",
                                    Value = 50
                                }
                            }
                        }
                    },
                    Actions = new List<RuleAction>()
                }
            }
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.Contains(result.Errors, e => e.Message.Contains("Must have at least one action"));
    }

    private RuleDefinitionModel CreateValidRule(string name)
    {
        return new RuleDefinitionModel
        {
            Name = name,
            Conditions = new ConditionGroupDefinition
            {
                All = new List<ConditionDefinition>
                {
                    new ConditionDefinition
                    {
                        Condition = new ComparisonConditionDefinition
                        {
                            Type = "comparison",
                            DataSource = "temperature",
                            Operator = ">",
                            Value = "50"
                        }
                    }
                }
            },
            Actions = new List<RuleAction>
            {
                new RuleAction
                {
                    SetValue = new SetValueAction
                    {
                        Key = "output",
                        Value = "1"
                    }
                }
            }
        };
    }
}
