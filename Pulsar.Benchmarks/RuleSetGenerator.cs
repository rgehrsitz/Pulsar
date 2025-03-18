using System.Text;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;

namespace Pulsar.Benchmarks
{
    public class RuleSetGenerator
    {
        private readonly DslParser _parser;
        private readonly List<string> _validSensors;

        public RuleSetGenerator()
        {
            _parser = new DslParser();
            _validSensors = GenerateValidSensors(100);
        }

        public List<RuleDefinition> GenerateRules(int count, int complexity = 1)
        {
            // Build the YAML content based on count and complexity
            var yaml = GenerateRulesYaml(count, complexity);

            // Parse the YAML into rule definitions
            return _parser.ParseRules(yaml, _validSensors, "benchmark.yaml").ToList();
        }

        private string GenerateRulesYaml(int count, int complexity)
        {
            var sb = new StringBuilder();
            sb.AppendLine("rules:");

            for (int i = 0; i < count; i++)
            {
                switch (complexity)
                {
                    case 1:
                        sb.Append(GenerateSimpleRule(i));
                        break;
                    case 2:
                        sb.Append(GenerateComplexRule(i));
                        break;
                    case 3:
                        sb.Append(GenerateTemporalRule(i));
                        break;
                    default:
                        sb.Append(GenerateSimpleRule(i));
                        break;
                }
            }

            return sb.ToString();
        }

        private string GenerateSimpleRule(int index)
        {
            return $@"  - name: SimpleRule{index}
    description: Simple benchmark rule {index}
    conditions:
      all:
        - condition:
            type: comparison
            sensor: sensor{index % 100}
            operator: greater_than
            value: {index % 1000}
    actions:
      - set_value:
          key: output{index}
          value: 1
";
        }

        private string GenerateComplexRule(int index)
        {
            return $@"  - name: ComplexRule{index}
    description: Complex benchmark rule {index}
    conditions:
      any:
        - all:
            - condition:
                type: comparison
                sensor: sensor{index % 50}
                operator: greater_than
                value: {index % 500}
            - condition:
                type: comparison
                sensor: sensor{(index + 25) % 100}
                operator: less_than
                value: {(index + 500) % 1000}
        - all:
            - condition:
                type: expression
                expression: sensor{index % 20} + sensor{(index + 10) % 100} > {index % 200}
            - condition:
                type: comparison
                sensor: sensor{(index + 5) % 100}
                operator: not_equal_to
                value: 0
    actions:
      - set_value:
          key: output{index}
          value: 1
";
        }

        private string GenerateTemporalRule(int index)
        {
            return $@"  - name: TemporalRule{index}
    description: Temporal benchmark rule {index}
    conditions:
      all:
        - condition:
            type: temporal
            sensor: sensor{index % 50}
            duration: 5m
            operator: increased_by
            threshold: {index % 100}
        - condition:
            type: temporal
            sensor: sensor{(index + 25) % 100}
            duration: 10m
            operator: max_gt
            threshold: {index % 500 + 500}
    actions:
      - set_value:
          key: output{index}
          value_expression: sensor{index % 100} * 2
";
        }

        private List<string> GenerateValidSensors(int count)
        {
            var sensors = new List<string>();
            for (int i = 0; i < count; i++)
            {
                sensors.Add($"sensor{i}");
            }
            for (int i = 0; i < count; i++)
            {
                sensors.Add($"output{i}");
            }
            return sensors;
        }
    }
}
