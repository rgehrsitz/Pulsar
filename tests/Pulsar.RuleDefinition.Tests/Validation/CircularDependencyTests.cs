using System.Collections.Generic;
using System.Linq;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Actions;
using Pulsar.RuleDefinition.Models.Conditions;
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
                                    DataSource = "output2",
                                    Operator = ">",
                                    Value = "0"
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
                                Key = "output1",
                                Value = "1"
                            }
                        }
                    }
                },
                new RuleDefinitionModel
                {
                    Name = "Rule2",
                    Conditions = new ConditionGroupDefinition
                    {
                        All = new List<ConditionDefinition>
                        {
                            new ConditionDefinition
                            {
                                Condition = new ComparisonConditionDefinition
                                {
                                    Type = "comparison",
                                    DataSource = "output1",
                                    Operator = ">",
                                    Value = "0"
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
                                Key = "output2",
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
        Assert.Contains(result.Errors, e => e.Contains("Circular dependency detected"));
    }

    [Fact]
    public void ValidateRuleSet_NoCircularDependency_NoErrors()
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
                                    DataSource = "input",
                                    Operator = ">",
                                    Value = "0"
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
                                Key = "temp1",
                                Value = "1"
                            }
                        }
                    }
                },
                new RuleDefinitionModel
                {
                    Name = "Rule2",
                    Conditions = new ConditionGroupDefinition
                    {
                        All = new List<ConditionDefinition>
                        {
                            new ConditionDefinition
                            {
                                Condition = new ComparisonConditionDefinition
                                {
                                    Type = "comparison",
                                    DataSource = "temp1",
                                    Operator = ">",
                                    Value = "0"
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
                                Key = "temp2",
                                Value = "1"
                            }
                        }
                    }
                },
                new RuleDefinitionModel
                {
                    Name = "Rule3",
                    Conditions = new ConditionGroupDefinition
                    {
                        All = new List<ConditionDefinition>
                        {
                            new ConditionDefinition
                            {
                                Condition = new ComparisonConditionDefinition
                                {
                                    Type = "comparison",
                                    DataSource = "temp2",
                                    Operator = ">",
                                    Value = "0"
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
                                Key = "result",
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
}
