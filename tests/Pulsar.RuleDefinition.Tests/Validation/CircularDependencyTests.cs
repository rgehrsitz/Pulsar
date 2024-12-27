using System.Collections.Generic;
using System.Linq;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Actions;
using Pulsar.RuleDefinition.Validation;
using Xunit;

namespace Pulsar.RuleDefinition.Tests.Validation;

public class CircularDependencyTests
{
    private readonly RuleValidator _validator;

    public CircularDependencyTests()
    {
        var config = new SystemConfig
        {
            Version = 1,
            ValidSensors = new List<string> { "temperature", "humidity", "pressure" }
        };
        _validator = new RuleValidator(config);
    }

    [Fact]
    public void ValidateRuleSet_DetectsCircularDependency()
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
                    Conditions = new ConditionGroup
                    {
                        All = new List<Condition>
                        {
                            new ComparisonCondition
                            {
                                DataSource = "temperature",
                                Operator = ">",
                                Value = 30
                            }
                        }
                    },
                    Actions = new List<RuleAction>
                    {
                        new RuleAction { SetValue = new SetValueAction { Key = "humidity", Value = 50 } }
                    }
                },
                new()
                {
                    Name = "Rule2",
                    Conditions = new ConditionGroup
                    {
                        All = new List<Condition>
                        {
                            new ComparisonCondition
                            {
                                DataSource = "humidity",
                                Operator = ">",
                                Value = 40
                            }
                        }
                    },
                    Actions = new List<RuleAction>
                    {
                        new RuleAction { SetValue = new SetValueAction { Key = "pressure", Value = 1000 } }
                    }
                },
                new()
                {
                    Name = "Rule3",
                    Conditions = new ConditionGroup
                    {
                        All = new List<Condition>
                        {
                            new ComparisonCondition
                            {
                                DataSource = "pressure",
                                Operator = ">",
                                Value = 900
                            }
                        }
                    },
                    Actions = new List<RuleAction>
                    {
                        new RuleAction { SetValue = new SetValueAction { Key = "temperature", Value = 25 } }
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.False(result.IsValid);
        var cyclicError = result.Errors.FirstOrDefault(e => e.Message.Contains("Cyclic dependency"));
        Assert.NotNull(cyclicError);
    }

    [Fact]
    public void ValidateRuleSet_AcceptsValidDependencies()
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
                    Conditions = new ConditionGroup
                    {
                        All = new List<Condition>
                        {
                            new ComparisonCondition
                            {
                                DataSource = "temperature",
                                Operator = ">",
                                Value = 30
                            }
                        }
                    },
                    Actions = new List<RuleAction>
                    {
                        new RuleAction { SetValue = new SetValueAction { Key = "humidity", Value = 50 } }
                    }
                },
                new()
                {
                    Name = "Rule2",
                    Conditions = new ConditionGroup
                    {
                        All = new List<Condition>
                        {
                            new ComparisonCondition
                            {
                                DataSource = "humidity",
                                Operator = ">",
                                Value = 40
                            }
                        }
                    },
                    Actions = new List<RuleAction>
                    {
                        new RuleAction { SetValue = new SetValueAction { Key = "pressure", Value = 1000 } }
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateRuleSet(ruleSet);

        // Assert
        Assert.True(result.IsValid);
    }
}
