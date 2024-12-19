using Pulsar.RuleDefinition.Analysis;
using Pulsar.RuleDefinition.Models;

namespace Pulsar.RuleDefinition.Tests.Analysis;

public class DependencyAnalyzerTests
{
    private readonly DependencyAnalyzer _analyzer;

    public DependencyAnalyzerTests()
    {
        _analyzer = new DependencyAnalyzer();
    }

    [Fact]
    public void AnalyzeAndOrder_NoDependencies_ReturnsOriginalOrder()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            ValidDataSources = new List<string> { "temperature", "humidity" },
            Rules = new List<Rule>
            {
                CreateRule("Rule1", "temperature > 50", "output1"),
                CreateRule("Rule2", "humidity > 80", "output2")
            }
        };

        // Act
        var (orderedRules, cyclicDependencies) = _analyzer.AnalyzeAndOrder(ruleSet);

        // Assert
        Assert.Empty(cyclicDependencies);
        Assert.Equal(2, orderedRules.Count);
        Assert.Equal(ruleSet.Rules.Select(r => r.Name), orderedRules.Select(r => r.Name));
    }

    [Fact]
    public void AnalyzeAndOrder_SimpleDependency_ReturnsCorrectOrder()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            ValidDataSources = new List<string> { "temperature", "derived_temp" },
            Rules = new List<Rule>
            {
                CreateRule("Rule2", "derived_temp > 100", "output2"),
                CreateRule("Rule1", "temperature > 50", "derived_temp")
            }
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
    public void AnalyzeAndOrder_CyclicDependency_ReturnsError()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            ValidDataSources = new List<string> { "value1", "value2" },
            Rules = new List<Rule>
            {
                CreateRule("Rule1", "value2 > 0", "value1"),
                CreateRule("Rule2", "value1 > 0", "value2")
            }
        };

        // Act
        var (orderedRules, cyclicDependencies) = _analyzer.AnalyzeAndOrder(ruleSet);

        // Assert
        Assert.NotEmpty(cyclicDependencies);
        Assert.Contains(cyclicDependencies, cd => cd.Contains("Cyclic dependency"));
    }

    [Fact]
    public void AnalyzeAndOrder_ComplexDependencies_ReturnsCorrectOrder()
    {
        // Arrange
        var ruleSet = new RuleSetDefinition
        {
            Version = 1,
            ValidDataSources = new List<string> { "input", "temp1", "temp2", "result" },
            Rules = new List<Rule>
            {
                CreateRule("Rule3", "temp2 > 0", "result"),
                CreateRule("Rule1", "input > 0", "temp1"),
                CreateRule("Rule2", "temp1 > 0", "temp2")
            }
        };

        // Act
        var (orderedRules, cyclicDependencies) = _analyzer.AnalyzeAndOrder(ruleSet);

        // Assert
        Assert.Empty(cyclicDependencies);
        Assert.Equal(3, orderedRules.Count);
        Assert.Equal(new[] { "Rule1", "Rule2", "Rule3" }, orderedRules.Select(r => r.Name));
    }

    private static Rule CreateRule(string name, string condition, string outputKey)
    {
        return new Rule
        {
            Name = name,
            Conditions = new ConditionGroup
            {
                All = new List<Condition>
                {
                    new ExpressionCondition
                    {
                        Type = "expression",
                        Expression = condition
                    }
                }
            },
            Actions = new List<RuleAction>
            {
                new()
                {
                    SetValue = new Dictionary<string, object>
                    {
                        { "key", outputKey },
                        { "value", "1" }
                    }
                }
            }
        };
    }
}
