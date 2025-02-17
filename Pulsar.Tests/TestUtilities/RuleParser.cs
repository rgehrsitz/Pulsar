// File: Pulsar.Tests/TestUtilities/RuleParser.cs

using System;
using System.Collections.Generic;
using Pulsar.Compiler;
using Pulsar.Compiler.Models;
using Serilog;

namespace Pulsar.Tests.TestUtilities
{
    public static class RuleParser
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        public static ParseResult Parse(string ruleContent)
        {
            try
            {
                _logger.Debug("Parsing rule content: {Content}", ruleContent);

                // This is a mock implementation for testing
                if (ruleContent.Contains("invalid"))
                {
                    _logger.Error("Invalid rule content detected");
                    return new ParseResult
                    {
                        IsValid = false,
                        Errors = new[] { "Invalid rule content" }
                    };
                }

                var rule = new RuleDefinition
                {
                    Name = "TestRule",
                    Description = "Test rule for mock parsing",
                    Conditions = new ConditionGroup
                    {
                        All = new List<ConditionDefinition>
                        {
                            new ComparisonCondition
                            {
                                Sensor = "TestSensor",
                                Operator = ComparisonOperator.GreaterThan,
                                Value = 0
                            }
                        }
                    },
                    Actions = new List<ActionDefinition>
                    {
                        new SetValueAction
                        {
                            Key = "TestOutput",
                            Value = 1.0
                        }
                    }
                };

                _logger.Debug("Successfully parsed rule: {RuleName}", rule.Name);

                return new ParseResult
                {
                    IsValid = true,
                    Rule = rule
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error parsing rule content");
                return new ParseResult
                {
                    IsValid = false,
                    Errors = new[] { ex.Message }
                };
            }
        }
    }

    public class ParseResult
    {
        public bool IsValid { get; set; }
        public RuleDefinition? Rule { get; set; }
        public string[] Errors { get; set; } = Array.Empty<string>();
    }
}
