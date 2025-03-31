// File: Pulsar.Tests/Parsing/ComplexRuleParsingTests.cs

using Pulsar.Compiler.Exceptions;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Serilog;

namespace Pulsar.Tests.Parsing
{
    public class ComplexRuleParsingTests
    {
        private readonly ILogger _logger = Pulsar.Tests.TestUtilities.LoggingConfig.ToSerilogLogger(
            Pulsar.Tests.TestUtilities.LoggingConfig.GetLogger()
        );

        private readonly DslParser _parser = new();

        private readonly List<string> _validSensors = new()
        {
            "temp1",
            "temp2",
            "pressure",
            "humidity",
            "ambient_pressure",
            "flow_rate",
            "valve_position",
        };

        [Fact]
        public void Parse_ComplexConditions_Succeeds()
        {
            // Arrange

            var ruleContent =
                @"

rules:

  - name: ComplexTempRule

    description: Complex temperature monitoring

    conditions:

      all:

        - condition:

            type: comparison

            sensor: temp1

            operator: greater_than

            value: 30.0

        - condition:

            type: comparison

            sensor: temp2

            operator: less_than

            value: 25.0

      any:

        - condition:

            type: threshold_over_time

            sensor: pressure

            threshold: 100.0

            duration: 300000  # 5 minutes

        - condition:

            type: expression

            expression: flow_rate * valve_position > 500

    actions:

      - set_value:

          key: temp_alert

          value: 1

      - send_message:

          channel: alerts

          message: Temperature differential detected";

            // Act

            var rules = _parser.ParseRules(ruleContent, _validSensors, "test.yaml");

            // Assert

            Assert.Single(rules);

            var rule = rules[0];

            // Check conditions

            Assert.NotNull(rule.Conditions);

            Assert.NotNull(rule.Conditions.All);

            Assert.NotNull(rule.Conditions.Any);

            Assert.Equal(2, rule.Conditions.All.Count);

            Assert.Equal(2, rule.Conditions.Any.Count);

            var temp1Condition = rule.Conditions.All[0] as ComparisonCondition;

            Assert.NotNull(temp1Condition);

            Assert.Equal("temp1", temp1Condition.Sensor);

            Assert.Equal(ComparisonOperator.GreaterThan, temp1Condition.Operator);

            Assert.Equal(30.0, temp1Condition.Value);

            var temp2Condition = rule.Conditions.All[1] as ComparisonCondition;

            Assert.NotNull(temp2Condition);

            Assert.Equal("temp2", temp2Condition.Sensor);

            Assert.Equal(ComparisonOperator.LessThan, temp2Condition.Operator);

            Assert.Equal(25.0, temp2Condition.Value);

            var temporalCondition = rule.Conditions.Any[0] as ThresholdOverTimeCondition;

            Assert.NotNull(temporalCondition);

            Assert.Equal("pressure", temporalCondition.Sensor);

            Assert.Equal(100.0, temporalCondition.Threshold);

            Assert.Equal(300000, temporalCondition.Duration);

            Assert.Equal(ThresholdOverTimeMode.Strict, temporalCondition.Mode);

            var expressionCondition = rule.Conditions.Any[1] as ExpressionCondition;

            Assert.NotNull(expressionCondition);

            Assert.Contains("flow_rate", expressionCondition.Expression);

            // Check actions - order matters

            Assert.NotNull(rule.Actions);

            Assert.Equal(2, rule.Actions.Count);

            var setValue = rule.Actions[0] as SetValueAction;

            Assert.NotNull(setValue);

            Assert.Equal("temp_alert", setValue.Key);

            Assert.Equal(1, Convert.ToInt32(setValue.Value));

            var sendMessage = rule.Actions[1] as SendMessageAction;

            Assert.NotNull(sendMessage);

            Assert.Equal("alerts", sendMessage.Channel);

            Assert.NotEmpty(sendMessage.Message);
        }

        [Fact]
        public void Parse_NestedConditionGroups_Succeeds()
        {
            // Arrange

            var ruleContent =
                @"

rules:

  - name: NestedConditionsRule

    description: Rule with nested condition groups

    conditions:

      all:

        - condition:

            type: group

            all:

              - condition:

                  type: comparison

                  sensor: temp1

                  operator: greater_than

                  value: 30

              - condition:

                  type: comparison

                  sensor: temp2

                  operator: greater_than

                  value: 30

        - condition:

            type: group

            any:

              - condition:

                  type: comparison

                  sensor: pressure

                  operator: less_than

                  value: 90

              - condition:

                  type: comparison

                  sensor: humidity

                  operator: greater_than

                  value: 60

    actions:

      - set_value:

          key: system_alert

          value: 1";

            // Act

            var rules = _parser.ParseRules(ruleContent, _validSensors, "test.yaml");

            // Assert

            Assert.Single(rules);

            var rule = rules[0];

            Assert.NotNull(rule.Conditions);

            Assert.NotNull(rule.Conditions.All);

            Assert.Equal(2, rule.Conditions.All.Count);

            var firstGroup = rule.Conditions.All[0] as ConditionGroup;

            Assert.NotNull(firstGroup);

            Assert.NotNull(firstGroup.All);

            Assert.Equal(2, firstGroup.All.Count);

            Assert.Empty(firstGroup.Any);

            var secondGroup = rule.Conditions.All[1] as ConditionGroup;

            Assert.NotNull(secondGroup);

            Assert.NotNull(secondGroup.All);

            Assert.NotNull(secondGroup.Any);

            Assert.Empty(secondGroup.All);

            Assert.Equal(2, secondGroup.Any.Count);

            // Check nested conditions

            var temp1Condition = firstGroup.All[0] as ComparisonCondition;

            Assert.NotNull(temp1Condition);

            Assert.Equal("temp1", temp1Condition.Sensor);

            Assert.Equal(ComparisonOperator.GreaterThan, temp1Condition.Operator);

            var pressureCondition = secondGroup.Any[0] as ComparisonCondition;

            Assert.NotNull(pressureCondition);

            Assert.Equal("pressure", pressureCondition.Sensor);

            Assert.Equal(ComparisonOperator.LessThan, pressureCondition.Operator);
        }

        [Fact]
        public void Parse_ComplexExpressions_Succeeds()
        {
            // Arrange

            var ruleContent =
                @"

rules:

  - name: ComplexExpressionRule

    description: Rule with complex mathematical expressions

    conditions:

      all:

        - condition:

            type: expression

            expression: (temp1 + temp2) / 2 > 30 && pressure * 1.5 < Max(150, ambient_pressure)

        - condition:

            type: expression

            expression: Abs(temp1 - temp2) > 10 || flow_rate < Min(50, valve_position * 2)

    actions:

      - set_value:

          key: expression_alert

          value_expression: temp1 * 0.6 + temp2 * 0.4";

            // Act

            var rules = _parser.ParseRules(ruleContent, _validSensors, "test.yaml");

            // Assert

            Assert.Single(rules);

            var rule = rules[0];

            Assert.NotNull(rule.Conditions);

            Assert.NotNull(rule.Conditions.All);

            Assert.Equal(2, rule.Conditions.All.Count);

            var expr1 = rule.Conditions.All[0] as ExpressionCondition;

            Assert.NotNull(expr1);

            Assert.Contains("Max", expr1.Expression);

            Assert.Contains("ambient_pressure", expr1.Expression);

            var expr2 = rule.Conditions.All[1] as ExpressionCondition;

            Assert.NotNull(expr2);

            Assert.Contains("Abs", expr2.Expression);

            Assert.Contains("Min", expr2.Expression);

            // Check that all referenced sensors are valid

            var allSensors = new[]
            {
                "temp1",
                "temp2",
                "pressure",
                "ambient_pressure",
                "flow_rate",
                "valve_position",
            };

            foreach (var sensor in allSensors)
            {
                Assert.Contains(sensor, _validSensors);
            }

            // Check that the value_expression in set_value action is valid

            var setValue = rule.Actions[0] as SetValueAction;

            Assert.NotNull(setValue);

            Assert.NotNull(setValue.ValueExpression);

            Assert.Contains("temp1", setValue.ValueExpression);

            Assert.Contains("temp2", setValue.ValueExpression);
        }

        [Fact]
        public void Parse_TemporalConditions_ValidatesMode()
        {
            // Arrange

            var ruleContent =
                @"

rules:

  - name: TemporalRule

    description: Rule with temporal conditions in different modes

    conditions:

      all:

        - condition:

            type: threshold_over_time

            sensor: temp1

            threshold: 30

            duration: 300000  # 5 minutes

            mode: strict      # Default mode

        - condition:

            type: threshold_over_time

            sensor: temp2

            threshold: 25

            duration: 60000   # 1 minute

            mode: extended    # Use last known value

    actions:

      - set_value:

          key: temp_alert

          value: 1";

            // Act

            var rules = _parser.ParseRules(ruleContent, _validSensors, "test.yaml");

            // Assert

            Assert.Single(rules);

            var rule = rules[0];

            Assert.NotNull(rule.Conditions);

            Assert.NotNull(rule.Conditions.All);

            Assert.Equal(2, rule.Conditions.All.Count);

            var strictCondition = rule.Conditions.All[0] as ThresholdOverTimeCondition;

            Assert.NotNull(strictCondition);

            Assert.Equal(ThresholdOverTimeMode.Strict, strictCondition.Mode);

            Assert.Equal(300000, strictCondition.Duration);

            var extendedCondition = rule.Conditions.All[1] as ThresholdOverTimeCondition;

            Assert.NotNull(extendedCondition);

            Assert.Equal(ThresholdOverTimeMode.Extended, extendedCondition.Mode);

            Assert.Equal(60000, extendedCondition.Duration);
        }

        [Fact]
        public void Parse_InvalidTemporalMode_ThrowsValidationException()
        {
            // Arrange

            var ruleContent =
                @"

rules:

  - name: InvalidTemporalRule

    description: Rule with invalid temporal mode

    conditions:

      all:

        - condition:

            type: threshold_over_time

            sensor: temp1

            threshold: 30

            duration: 300000

            mode: invalid_mode  # Only 'strict' or 'extended' are valid

    actions:

      - set_value:

          key: temp_alert

          value: 1";

            // Act & Assert

            var ex = Assert.Throws<ValidationException>(
                () => _parser.ParseRules(ruleContent, _validSensors, "test.yaml")
            );

            Assert.Contains("Invalid temporal mode", ex.Message);

            Assert.Contains("Must be 'strict' or 'extended'", ex.Message);
        }

        [Fact]
        public void Parse_DefaultTemporalMode_IsStrict()
        {
            // Arrange

            var ruleContent =
                @"

rules:

  - name: DefaultModeRule

    description: Rule with no temporal mode specified

    conditions:

      all:

        - condition:

            type: threshold_over_time

            sensor: temp1

            threshold: 30

            duration: 300000  # Mode not specified, should default to strict

    actions:

      - set_value:

          key: temp_alert

          value: 1";

            // Act

            var rules = _parser.ParseRules(ruleContent, _validSensors, "test.yaml");

            // Assert

            Assert.Single(rules);

            var rule = rules[0];

            Assert.NotNull(rule.Conditions);

            Assert.NotNull(rule.Conditions.All);

            Assert.Single(rule.Conditions.All);

            var condition = rule.Conditions.All[0] as ThresholdOverTimeCondition;

            Assert.NotNull(condition);

            Assert.Equal(ThresholdOverTimeMode.Strict, condition.Mode);
        }
    }
}
