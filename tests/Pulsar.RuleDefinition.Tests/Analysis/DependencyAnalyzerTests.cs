using System.Collections.Generic;
using Pulsar.RuleDefinition.Analysis;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Actions;
using Pulsar.RuleDefinition.Models.Conditions;
using Xunit;

namespace Pulsar.RuleDefinition.Tests.Analysis;

public class DependencyAnalyzerTests
{
    private readonly DependencyAnalyzer _analyzer;

    public DependencyAnalyzerTests()
    {
        _analyzer = new DependencyAnalyzer();
    }

    private RuleDefinitionModel CreateRule(string name, string condition, string outputKey)
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
                            DataSource = condition.Split(' ')[0],
                            Operator = condition.Split(' ')[1],
                            Value = condition.Split(' ')[2]
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
                        Key = outputKey,
                        Value = "1"
                    }
                }
            }
        };
    }

    [Fact]
    public void AnalyzeAndOrder_NoDependencies_ReturnsOriginalOrder()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<RuleDefinitionModel>
            {
                CreateRule("Rule1", "temperature > 50", "output1"),
                CreateRule("Rule2", "humidity > 80", "output2"),
            },
        };

        // Act
        var (orderedRules, cyclicDependencies) = _analyzer.AnalyzeAndOrder(ruleSet);

        // Assert
        Assert.Empty(cyclicDependencies);
        Assert.Equal(2, orderedRules.Count);
        Assert.Equal("Rule1", orderedRules[0].Name);
        Assert.Equal("Rule2", orderedRules[1].Name);
    }

    [Fact]
    public void AnalyzeAndOrder_WithDependencies_ReturnsCorrectOrder()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<RuleDefinitionModel>
            {
                CreateRule("Rule1", "temperature > 50", "output1"),
                CreateRule("Rule2", "output1 > 0", "output2"),
                CreateRule("Rule3", "output2 > 0", "output3"),
            },
        };

        // Act
        var (orderedRules, cyclicDependencies) = _analyzer.AnalyzeAndOrder(ruleSet);

        // Assert
        Assert.Empty(cyclicDependencies);
        Assert.Equal(3, orderedRules.Count);
        Assert.Equal("Rule1", orderedRules[0].Name);
        Assert.Equal("Rule2", orderedRules[1].Name);
        Assert.Equal("Rule3", orderedRules[2].Name);
    }

    [Fact]
    public void AnalyzeAndOrder_CircularDependency_ReturnsError()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<RuleDefinitionModel>
            {
                CreateRule("Rule1", "output2 > 0", "output1"),
                CreateRule("Rule2", "output1 > 0", "output2"),
            },
        };

        // Act
        var (orderedRules, cyclicDependencies) = _analyzer.AnalyzeAndOrder(ruleSet);

        // Assert
        Assert.NotEmpty(cyclicDependencies);
        Assert.Contains("Rule1", cyclicDependencies[0]);
        Assert.Contains("Rule2", cyclicDependencies[0]);
    }

    [Fact]
    public void AnalyzeAndOrder_ComplexDependencies_ReturnsCorrectOrder()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            Rules = new List<RuleDefinitionModel>
            {
                CreateRule("Rule1", "temperature > 50", "temp1"),
                CreateRule("Rule2", "temp1 > 0", "temp2"),
                CreateRule("Rule3", "humidity > 80", "humid1"),
                CreateRule("Rule4", "humid1 > 0", "humid2"),
                CreateRule("Rule5", "temp2 > 0", "result1"),
                CreateRule("Rule6", "humid2 > 0", "result2"),
            },
        };

        // Act
        var (orderedRules, cyclicDependencies) = _analyzer.AnalyzeAndOrder(ruleSet);

        // Assert
        Assert.Empty(cyclicDependencies);
        Assert.Equal(6, orderedRules.Count);

        // Check relative ordering
        Assert.True(GetIndex(orderedRules, "Rule1") < GetIndex(orderedRules, "Rule2"));
        Assert.True(GetIndex(orderedRules, "Rule2") < GetIndex(orderedRules, "Rule5"));
        Assert.True(GetIndex(orderedRules, "Rule3") < GetIndex(orderedRules, "Rule4"));
        Assert.True(GetIndex(orderedRules, "Rule4") < GetIndex(orderedRules, "Rule6"));
    }

    private int GetIndex(List<RuleDefinitionModel> list, string ruleName)
    {
        return list.FindIndex(r => r.Name == ruleName);
    }
}
