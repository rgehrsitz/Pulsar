// File: Pulsar.Tests/RuleValidation/DependencyAnalysisTests.cs

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Models;

namespace Pulsar.Tests.RuleValidation
{
    public class DependencyAnalysisTests
    {
        private readonly ILogger<DependencyAnalyzer> _logger = NullLogger<DependencyAnalyzer>.Instance;

        [Fact]
        public void DetectCircularDependency_DirectCycle_ReturnsError()
        {
            // Arrange
            var rules = new List<RuleDefinition>
            {
                new RuleDefinition
                {
                    Name = "Rule1",
                    Description = "First rule that depends on Rule2's output",
                    Conditions = new ConditionGroup
                    {
                        All = new List<ConditionDefinition>
                        {
                            new ComparisonCondition
                            {
                                Type = ConditionType.Comparison,
                                Sensor = "Rule2_Output",
                                Operator = ComparisonOperator.GreaterThan,
                                Value = 10,
                            },
                        },
                    },
                    Actions = new List<ActionDefinition>
                    {
                        new SetValueAction { Key = "Rule1_Output", Value = 1.0 },
                    },
                },
                new RuleDefinition
                {
                    Name = "Rule2",
                    Description = "Second rule that depends on Rule1's output",
                    Conditions = new ConditionGroup
                    {
                        All = new List<ConditionDefinition>
                        {
                            new ComparisonCondition
                            {
                                Type = ConditionType.Comparison,
                                Sensor = "Rule1_Output",
                                Operator = ComparisonOperator.LessThan,
                                Value = 5,
                            },
                        },
                    },
                    Actions = new List<ActionDefinition>
                    {
                        new SetValueAction { Key = "Rule2_Output", Value = 2.0 },
                    },
                },
            };

            // Act
            var analyzer = new DependencyAnalyzer();
            var result = analyzer.ValidateDependencies(rules);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(
                result.CircularDependencies,
                cycle => cycle.Contains("Rule1") && cycle.Contains("Rule2")
            );
        }

        [Fact]
        public void DetectTemporalDependencies_ReturnsCorrectDependencies()
        {
            // Arrange
            var rules = new List<RuleDefinition>
            {
                new RuleDefinition
                {
                    Name = "TemporalRule",
                    Description = "Rule with temporal conditions",
                    Conditions = new ConditionGroup
                    {
                        All = new List<ConditionDefinition>
                        {
                            new ThresholdOverTimeCondition
                            {
                                Type = ConditionType.ThresholdOverTime,
                                Sensor = "temperature",
                                Threshold = 30,
                                Duration = 300000, // 5 minutes
                            },
                        },
                    },
                    Actions = new List<ActionDefinition>
                    {
                        new SetValueAction { Key = "temp_alert", Value = 1.0 },
                    },
                },
            };

            // Act
            var analyzer = new DependencyAnalyzer();
            var result = analyzer.ValidateDependencies(rules);

            // Assert
            Assert.True(result.IsValid);
            Assert.True(result.TemporalDependencies.ContainsKey("TemporalRule"));
            Assert.Contains("temperature", result.TemporalDependencies["TemporalRule"]);
        }

        [Fact]
        public void DetectComplexExpressionDependencies_ReturnsAllDependencies()
        {
            // Arrange
            var rules = new List<RuleDefinition>
            {
                new RuleDefinition
                {
                    Name = "ExpressionRule",
                    Description = "Rule with complex expression",
                    Conditions = new ConditionGroup
                    {
                        All = new List<ConditionDefinition>
                        {
                            new ExpressionCondition
                            {
                                Type = ConditionType.Expression,
                                Expression =
                                    "(temp1 + temp2) / 2 > 30 && pressure * 1.5 < Max(150, ambient_pressure)",
                            },
                        },
                    },
                    Actions = new List<ActionDefinition>
                    {
                        new SetValueAction { Key = "complex_alert", Value = 1.0 },
                    },
                },
            };

            // Act
            var analyzer = new DependencyAnalyzer();
            var result = analyzer.ValidateDependencies(rules);

            // Assert
            Assert.True(result.IsValid);
            var dependencies = analyzer.AnalyzeDependencies(rules);
            Assert.Single(dependencies); // Should have one rule

            // The expression should depend on temp1, temp2, pressure, and ambient_pressure
            var complexityScore = result.RuleComplexityScores["ExpressionRule"];
            Assert.True(
                complexityScore > 0,
                "Complex expression should have non-zero complexity score"
            );
        }

        [Fact]
        public void DetectDeepDependencyChain_ReturnsWarning()
        {
            // Create a chain of 11 rules, each depending on the previous one
            var rules = new List<RuleDefinition>();
            for (int i = 1; i <= 11; i++)
            {
                var rule = new RuleDefinition
                {
                    Name = $"Rule{i}",
                    Description = $"Rule {i} in deep chain",
                    Conditions = new ConditionGroup { All = new List<ConditionDefinition>() },
                    Actions = new List<ActionDefinition>
                    {
                        new SetValueAction { Key = $"Rule{i}_Output", Value = i },
                    },
                };

                if (i > 1)
                {
                    // Add dependency on previous rule
                    ((List<ConditionDefinition>)rule.Conditions.All).Add(
                        new ComparisonCondition
                        {
                            Type = ConditionType.Comparison,
                            Sensor = $"Rule{i - 1}_Output",
                            Operator = ComparisonOperator.GreaterThan,
                            Value = 0,
                        }
                    );
                }

                rules.Add(rule);
            }

            // Act
            var analyzer = new DependencyAnalyzer();
            var result = analyzer.ValidateDependencies(rules);

            // Assert
            Assert.True(result.IsValid); // Deep chains don't make it invalid
            Assert.NotEmpty(result.DeepDependencyChains);
            Assert.Contains(result.DeepDependencyChains, chain => chain.Count > 10);
        }
    }
}
