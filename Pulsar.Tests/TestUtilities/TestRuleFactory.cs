// File: Pulsar.Tests/TestUtilities/TestRuleFactory.cs

using System;
using System.Collections.Generic;
using System.Threading;
using Pulsar.Compiler;
using Pulsar.Compiler.Models;
using Pulsar.Tests.TestUtilities;
using Serilog;

namespace Pulsar.Tests.TestUtilities
{
    /// <summary>
    /// Factory class for creating test rules. This is NOT a parser - it creates predefined rules for testing.
    /// For actual rule parsing, use DslParser from the Compiler project.
    /// </summary>
    public static class TestRuleFactory
    {
        // Use our adapter to convert MS Logger to Serilog
        private static readonly ILogger _logger = LoggingConfig.GetLogger().ToSerilogLogger();
        private static int _ruleCounter = 0;

        public static RuleDefinition CreateTestRule(string name = "", string description = "")
        {
            var ruleName = string.IsNullOrEmpty(name)
                ? $"TestRule_{Interlocked.Increment(ref _ruleCounter)}"
                : name;

            _logger.Debug("Creating test rule: {RuleName}", ruleName);

            return new RuleDefinition
            {
                Name = ruleName,
                Description = description ?? "Test rule created by TestRuleFactory",
                Conditions = new ConditionGroup
                {
                    All = new List<ConditionDefinition>
                    {
                        new ComparisonCondition
                        {
                            Type = ConditionType.Comparison,
                            Sensor = "TestSensor",
                            Operator = ComparisonOperator.GreaterThan,
                            Value = 0,
                        },
                    },
                },
                Actions = new List<ActionDefinition>
                {
                    new SetValueAction
                    {
                        Type = ActionType.SetValue,
                        Key = "TestOutput",
                        Value = 1.0,
                    },
                },
            };
        }

        public static RuleDefinition CreateInvalidRule()
        {
            _logger.Debug("Creating invalid test rule");

            return new RuleDefinition
            {
                // Missing required fields to make it invalid
                Name = "",
                Description = "Invalid test rule",
                Conditions = null,
                Actions = new List<ActionDefinition>(),
            };
        }

        public static RuleDefinition CreateComplexRule(
            string name,
            List<ConditionDefinition> conditions,
            List<ActionDefinition> actions
        )
        {
            _logger.Debug("Creating complex test rule: {RuleName}", name);

            return new RuleDefinition
            {
                Name = name,
                Description = "Complex test rule created by TestRuleFactory",
                Conditions = new ConditionGroup { All = conditions },
                Actions = actions,
            };
        }
    }
}
