// File: Pulsar.Tests/Parsing/RuleParsingTests.cs
using System;
using System.Collections.Generic;
using Xunit;
using Pulsar.Tests.TestUtilities;
using Serilog;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Pulsar.Compiler.Exceptions;
using YamlDotNet.Core;

namespace Pulsar.Tests.Parsing
{
    public class RuleParsingTests
    {
        private readonly ILogger _logger;
        private readonly DslParser _parser;
        private readonly List<string> _validSensors;

        public RuleParsingTests()
        {
            _logger = Pulsar.Tests.TestUtilities.LoggingConfig.ToSerilogLogger(Pulsar.Tests.TestUtilities.LoggingConfig.GetLogger());
            _parser = new DslParser();
            _validSensors = new List<string> { "temp", "pressure", "humidity" };
        }

        [Fact]
        public void Parse_ValidRule_Succeeds()
        {
            _logger.Debug("Starting valid rule parsing test");

            // Arrange
            var ruleContent = @"
rules:
  - name: TestRule
    description: A test rule
    conditions:
      all:
        - condition:
            type: comparison
            sensor: temp
            operator: greater_than
            value: 30.0
    actions:
      - set_value:
          key: alert
          value: 1.0";

            // Act
            var rules = _parser.ParseRules(ruleContent, _validSensors, "test.yaml");

            // Assert
            Assert.Single(rules);
            var rule = rules[0];
            Assert.Equal("TestRule", rule.Name);
            Assert.Equal("A test rule", rule.Description);
            
            Assert.NotNull(rule.Conditions);
            Assert.NotNull(rule.Conditions.All);
            Assert.Single(rule.Conditions.All);

            var condition = rule.Conditions.All[0] as ComparisonCondition;
            Assert.NotNull(condition);
            Assert.Equal("temp", condition.Sensor);
            Assert.Equal(ComparisonOperator.GreaterThan, condition.Operator);
            Assert.Equal(30.0, condition.Value);

            Assert.NotNull(rule.Actions);
            Assert.Single(rule.Actions);
            var action = rule.Actions[0] as SetValueAction;
            Assert.NotNull(action);
            Assert.Equal("alert", action.Key);
            Assert.Equal(1.0, action.Value);

            _logger.Debug("Valid rule parsing test completed successfully");
        }

        [Fact]
        public void Parse_InvalidYamlStructure_ThrowsException()
        {
            _logger.Debug("Starting invalid YAML structure test");

            // Arrange
            var ruleContent = @"
rules: [
  { this is clearly invalid
    YAML syntax
  name: TestRule
  conditions: *invalid-anchor
  actions: <<invalid-merge
";  // Multiple YAML syntax errors: unclosed brackets, invalid anchor, invalid merge

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() =>
                _parser.ParseRules(ruleContent, _validSensors, "test.yaml"));
            Assert.Contains("Error parsing YAML", ex.Message);

            _logger.Debug("Invalid YAML structure test completed with expected error");
        }

        [Fact]
        public void Parse_MissingRequiredFields_ThrowsValidationException()
        {
            _logger.Debug("Starting missing required fields test");

            // Arrange - Missing conditions
            var missingConditions = @"
rules:
  - name: TestRule
    description: Missing conditions
    actions:
      - set_value:
          key: alert
          value: 1.0";

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() =>
                _parser.ParseRules(missingConditions, _validSensors, "test.yaml"));
            Assert.Contains("must have at least one condition", ex.Message);

            // Arrange - Missing actions
            var missingActions = @"
rules:
  - name: TestRule
    description: Missing actions
    conditions:
      all:
        - condition:
            type: comparison
            sensor: temp
            operator: greater_than
            value: 30.0";

            // Act & Assert
            ex = Assert.Throws<ValidationException>(() =>
                _parser.ParseRules(missingActions, _validSensors, "test.yaml"));
            Assert.Contains("must have at least one action", ex.Message);

            _logger.Debug("Missing required fields test completed with expected errors");
        }

        [Fact]
        public void Parse_InvalidSensor_ThrowsValidationException()
        {
            _logger.Debug("Starting invalid sensor test");

            // Arrange
            var ruleContent = @"
rules:
  - name: TestRule
    description: Rule with invalid sensor
    conditions:
      all:
        - condition:
            type: comparison
            sensor: invalid_sensor
            operator: greater_than
            value: 30.0
    actions:
      - set_value:
          key: alert
          value: 1.0";

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() =>
                _parser.ParseRules(ruleContent, _validSensors, "test.yaml"));
            Assert.Contains("Invalid sensors found", ex.Message);

            _logger.Debug("Invalid sensor test completed with expected error");
        }

        [Fact]
        public void Parse_InvalidOperator_ThrowsValidationException()
        {
            _logger.Debug("Starting invalid operator test");

            // Arrange
            var ruleContent = @"
rules:
  - name: TestRule
    description: Rule with invalid operator
    conditions:
      all:
        - condition:
            type: comparison
            sensor: temp
            operator: invalid_operator
            value: 30.0
    actions:
      - set_value:
          key: alert
          value: 1.0";

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() =>
                _parser.ParseRules(ruleContent, _validSensors, "test.yaml"));
            Assert.Contains("Invalid operator", ex.Message);

            _logger.Debug("Invalid operator test completed with expected error");
        }

        [Fact]
        public void Parse_InvalidExpression_ThrowsValidationException()
        {
            _logger.Debug("Starting invalid expression test");

            // Arrange
            var ruleContent = @"
rules:
  - name: TestRule
    description: Rule with invalid expression
    conditions:
      all:
        - condition:
            type: expression
            expression: """"  # Empty expression
    actions:
      - set_value:
          key: alert
          value: 1.0";

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() =>
                _parser.ParseRules(ruleContent, _validSensors, "test.yaml"));
            Assert.Contains("Expression condition must specify an expression", ex.Message);

            _logger.Debug("Invalid expression test completed with expected error");
        }
    }
}
