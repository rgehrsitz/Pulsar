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
            Rules = new List<RuleDefinitionModel>
            {
                new RuleDefinitionModel
                {
                    Name = "Rule1",
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
                                Key = "alerts:temperature",
                                Value = "1"
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateRuleSet_InvalidVersion_ReturnsError()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 2,
            Rules = new List<RuleDefinitionModel>
            {
                new RuleDefinitionModel
                {
                    Name = "Rule1",
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
                                Key = "alerts:temperature",
                                Value = "1"
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Invalid version"));
    }

    [Fact]
    public void ValidateRuleSet_EmptyRules_ReturnsError()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<RuleDefinitionModel>()
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("must have at least one rule"));
    }

    [Fact]
    public void ValidateRuleSet_DuplicateRuleNames_ReturnsError()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<RuleDefinitionModel>
            {
                CreateValidRule("Rule1"),
                CreateValidRule("Rule1")
            }
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate rule names"));
    }

    [Fact]
    public void ValidateRule_InvalidDataSource_ReturnsError()
    {
        // Arrange
        var rule = new RuleDefinitionModel
        {
            Name = "Rule1",
            Conditions = new ConditionGroupDefinition
            {
                All = new List<ConditionDefinition>
                {
                    new ConditionDefinition
                    {
                        Condition = new ComparisonConditionDefinition
                        {
                            Type = "comparison",
                            DataSource = "invalid_sensor",
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
                        Key = "alerts:temperature",
                        Value = "1"
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateRule(rule);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Invalid data source"));
    }

    [Fact]
    public void ValidateRule_InvalidOperator_ReturnsError()
    {
        // Arrange
        var rule = new RuleDefinitionModel
        {
            Name = "Rule1",
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
                            Operator = "invalid",
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
                        Key = "alerts:temperature",
                        Value = "1"
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateRule(rule);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Invalid operator"));
    }

    [Fact]
    public void ValidateRule_NoConditions_ReturnsError()
    {
        // Arrange
        var rule = new RuleDefinitionModel
        {
            Name = "Rule1",
            Conditions = new ConditionGroupDefinition(),
            Actions = new List<RuleAction>
            {
                new RuleAction
                {
                    SetValue = new SetValueAction
                    {
                        Key = "alerts:temperature",
                        Value = "1"
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateRule(rule);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Must have at least one condition"));
    }

    [Fact]
    public void ValidateRule_NoActions_ReturnsError()
    {
        // Arrange
        var rule = new RuleDefinitionModel
        {
            Name = "Rule1",
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
            Actions = new List<RuleAction>()
        };

        // Act
        var result = _validator.ValidateRule(rule);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Must have at least one action"));
    }

    [Fact]
    public void ValidateRuleSet_ValidRule_ReturnsValidResult()
    {
        // Arrange
        var rule = new RuleDefinitionModel
        {
            Name = "TestRule",
            Conditions = new ConditionGroupDefinition
            {
                Conditions = new List<Condition>
                {
                    new ThresholdConditionDefinition
                    {
                        DataSource = "temperature",
                        Threshold = "50",
                        Operator = ">"
                    }
                }
            },
            Actions = new List<ActionDefinition>
            {
                new ActionDefinition { Type = "alert", Parameters = new Dictionary<string, string> { { "message", "High temperature!" } } }
            }
        };

        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<RuleDefinitionModel> { rule }
        };

        // Act
        var (isValid, errors) = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRuleSet_InvalidRule_ReturnsErrors()
    {
        // Arrange
        var rule = new RuleDefinitionModel
        {
            Name = "",  // Invalid: empty name
            Conditions = new ConditionGroupDefinition
            {
                Conditions = new List<Condition>
                {
                    new ThresholdConditionDefinition
                    {
                        DataSource = "",  // Invalid: empty data source
                        Threshold = "",   // Invalid: empty threshold
                        Operator = ">"
                    }
                }
            },
            Actions = new List<ActionDefinition>()  // Invalid: no actions
        };

        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<RuleDefinitionModel> { rule }
        };

        // Act
        var (isValid, errors) = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("name"));
        Assert.Contains(errors, e => e.Contains("data source"));
        Assert.Contains(errors, e => e.Contains("threshold"));
        Assert.Contains(errors, e => e.Contains("actions"));
    }

    [Fact]
    public void ValidateRuleSet_CircularDependency_ReturnsError()
    {
        // Arrange
        var rule1 = new RuleDefinitionModel
        {
            Name = "Rule1",
            Conditions = new ConditionGroupDefinition
            {
                Conditions = new List<Condition>
                {
                    new ThresholdConditionDefinition
                    {
                        DataSource = "output2",  // Depends on Rule2
                        Threshold = "0",
                        Operator = ">"
                    }
                }
            },
            Actions = new List<ActionDefinition>
            {
                new ActionDefinition { Type = "set", Parameters = new Dictionary<string, string> { { "output", "output1" } } }
            }
        };

        var rule2 = new RuleDefinitionModel
        {
            Name = "Rule2",
            Conditions = new ConditionGroupDefinition
            {
                Conditions = new List<Condition>
                {
                    new ThresholdConditionDefinition
                    {
                        DataSource = "output1",  // Depends on Rule1
                        Threshold = "0",
                        Operator = ">"
                    }
                }
            },
            Actions = new List<ActionDefinition>
            {
                new ActionDefinition { Type = "set", Parameters = new Dictionary<string, string> { { "output", "output2" } } }
            }
        };

        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<RuleDefinitionModel> { rule1, rule2 }
        };

        // Act
        var (isValid, errors) = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("circular"));
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
                        Key = "alerts:temperature",
                        Value = "1"
                    }
                }
            }
        };
    }
}
