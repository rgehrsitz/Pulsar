namespace Pulsar.Tests.Integration.Helpers
{
    /// <summary>
    /// Contains YAML templates for Beacon rules used in tests
    /// </summary>
    public static class BeaconRuleTemplates
    {
        /// <summary>
        /// Returns a simple rule YAML that sets a flag when temperature exceeds a threshold
        /// </summary>
        public static string SimpleRuleYaml()
        {
            return @"rules:
  - name: SimpleTemperatureRule
    description: Sets a flag when temperature exceeds threshold
    conditions:
      all:
        - condition:
            type: comparison
            sensor: input:temperature
            operator: '>'
            value: 30
    actions:
      - set_value:
          key: output:high_temperature
          value_expression: 'true'";
        }

        /// <summary>
        /// Returns a temporal rule YAML that detects when temperature rises over time
        /// </summary>
        public static string TemporalRuleYaml()
        {
            return @"rules:
  - name: TemperatureRisingRule
    description: Detects when temperature rises by 10 degrees over time
    conditions:
      all:
        - condition:
            type: threshold_over_time
            sensor: input:temperature
            operator: '>'
            threshold: 30
            duration: 1000  # 1 second duration for test purposes
    actions:
      - set_value:
          key: output:temperature_rising
          value_expression: 'true'
      - set_value:
          key: buffer:temp_history
          value_expression: 'input:temperature'";
        }

        /// <summary>
        /// Returns a complex rule YAML that demonstrates multi-condition logic
        /// </summary>
        public static string ComplexRuleYaml()
        {
            return @"rules:
  - name: ComplexConditionRule
    description: Demonstrates a rule with multiple conditions
    conditions:
      all:
        - condition:
            type: comparison
            sensor: input:temperature
            operator: '>'
            value: 30
        - condition:
            type: comparison
            sensor: input:humidity
            operator: '>'
            value: 60
    actions:
      - set_value:
          key: output:high_temp_and_humidity
          value_expression: 'true'
      - send_message:
          channel: alerts
          message: ""High temperature and humidity detected!""";
        }

        /// <summary>
        /// Returns a rule that uses expressions for calculations
        /// </summary>
        public static string ExpressionRuleYaml()
        {
            return @"rules:
  - name: ExpressionRule
    description: Uses expressions to calculate values
    conditions:
      all:
        - condition:
            type: expression
            expression: 'input:temperature > 25 && input:humidity > 50'
    actions:
      - set_value:
          key: output:heat_index
          value_expression: '0.5 * (input:temperature + 61.0 + ((input:temperature - 68.0) * 1.2) + (input:humidity * 0.094))'";
        }

        /// <summary>
        /// Returns a rule that uses dependencies between rules
        /// </summary>
        public static string DependentRuleYaml()
        {
            return @"rules:
  - name: PrimaryRule
    description: First rule that computes a value
    conditions:
      all:
        - condition:
            type: comparison
            sensor: input:temperature
            operator: '>'
            value: 20
    actions:
      - set_value:
          key: output:normalized_temp
          value_expression: 'input:temperature / 100.0'

  - name: DependentRule
    description: Rule that depends on output from another rule
    conditions:
      all:
        - condition:
            type: comparison
            sensor: output:normalized_temp
            operator: '>'
            value: 0.25
    actions:
      - set_value:
          key: output:temp_alert_level
          value_expression: 'output:normalized_temp * 10'";
        }
        
        /// <summary>
        /// Returns a rule that uses string comparisons
        /// </summary>
        public static string StringComparisonRuleYaml()
        {
            return @"rules:
  - name: StringComparisonRule
    description: Tests string comparison operations
    conditions:
      all:
        - condition:
            type: expression
            expression: 'input:status == ""active""'
    actions:
      - set_value:
          key: output:status_active
          value_expression: 'true'
      - send_message:
          channel: logs
          message: ""System status is active""";
        }
        
        /// <summary>
        /// Returns a rule that uses string operations and concatenation
        /// </summary>
        public static string StringOperationsRuleYaml()
        {
            return @"rules:
  - name: StringOperationsRule
    description: Tests string operations and concatenation
    conditions:
      all:
        - condition:
            type: comparison
            sensor: input:temperature
            operator: '>'
            value: 30
    actions:
      - set_value:
          key: output:status_message
          value: ""Temperature exceeded threshold: 30°C""
      - send_message:
          channel: notifications
          message_expression: '""Alert: Temperature value "" + input:temperature + ""°C exceeds threshold""'";
        }
        
        /// <summary>
        /// Returns a rule that uses logical operators in expressions
        /// </summary>
        public static string LogicalOperatorsRuleYaml()
        {
            return @"rules:
  - name: LogicalOperatorsRule
    description: Tests logical operators in rule expressions
    conditions:
      all:
        - condition:
            type: expression
            expression: 'input:temperature > 25 and input:humidity > 60 or input:status == ""critical""'
    actions:
      - set_value:
          key: output:alert_condition
          value_expression: 'true'
      - send_message:
          channel: alerts
          message: ""Alert condition detected with logical operators""";
        }
    }
}