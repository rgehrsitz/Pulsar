using System;
using System.Text.RegularExpressions;
using Pulsar.RuleDefinition.Models.Conditions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Pulsar.RuleDefinition.Parser;

public class ConditionTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(Condition);

    public object ReadYaml(IParser parser, Type type)
    {
        // Start of the condition mapping
        parser.Consume<MappingStart>();

        // Read "condition:" key
        var conditionKey = parser.Consume<Scalar>().Value;
        if (conditionKey != "condition")
        {
            throw new YamlException($"Expected 'condition' key, got '{conditionKey}'");
        }

        // Start of the condition details mapping
        parser.Consume<MappingStart>();

        // Read the condition type
        parser.Consume<Scalar>(); // "type" key
        var conditionType = parser.Consume<Scalar>().Value;

        // Create the appropriate condition type
        Condition condition = conditionType switch
        {
            "comparison" => ParseComparisonCondition(parser),
            "threshold_over_time" => ParseThresholdOverTimeCondition(parser),
            "expression" => ParseExpressionCondition(parser),
            _ => throw new YamlException($"Unknown condition type: {conditionType}"),
        };

        condition.Type = conditionType;

        // End of condition details mapping
        parser.Consume<MappingEnd>();

        // End of condition mapping
        parser.Consume<MappingEnd>();

        return condition;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        throw new NotImplementedException("Writing YAML is not supported");
    }

    private ComparisonConditionDefinition ParseComparisonCondition(IParser parser)
    {
        var condition = new ComparisonConditionDefinition();

        while (parser.Current is Scalar scalar)
        {
            parser.MoveNext();
            var value = parser.Consume<Scalar>().Value;

            switch (scalar.Value)
            {
                case "sensor":
                    condition.DataSource = value;
                    break;
                case "operator":
                    condition.Operator = value;
                    break;
                case "value":
                    if (double.TryParse(value, out var doubleValue))
                    {
                        condition.Value = doubleValue;
                    }
                    else
                    {
                        throw new YamlException($"Invalid numeric value: {value}");
                    }
                    break;
                default:
                    throw new YamlException($"Unknown field: {scalar.Value}");
            }
        }

        return condition;
    }

    private ThresholdOverTimeConditionDefinition ParseThresholdOverTimeCondition(IParser parser)
    {
        var condition = new ThresholdOverTimeConditionDefinition();

        while (parser.Current is Scalar scalar)
        {
            parser.MoveNext();
            var value = parser.Consume<Scalar>().Value;

            switch (scalar.Value)
            {
                case "sensor":
                    condition.DataSource = value;
                    break;
                case "threshold":
                    if (double.TryParse(value, out var doubleValue))
                    {
                        condition.Threshold = doubleValue;
                    }
                    else
                    {
                        throw new YamlException($"Invalid numeric value: {value}");
                    }
                    break;
                case "duration":
                    if (TryParseTimeSpan(value, out var duration))
                    {
                        condition.Duration = duration;
                    }
                    else
                    {
                        throw new YamlException($"Invalid duration: {value}");
                    }
                    break;
                default:
                    throw new YamlException($"Unknown field: {scalar.Value}");
            }
        }

        return condition;
    }

    private ExpressionConditionDefinition ParseExpressionCondition(IParser parser)
    {
        var condition = new ExpressionConditionDefinition();

        while (parser.Current is Scalar scalar)
        {
            parser.MoveNext();
            var value = parser.Consume<Scalar>().Value;

            switch (scalar.Value)
            {
                case "expression":
                    condition.Expression = value;
                    break;
                default:
                    throw new YamlException($"Unknown field: {scalar.Value}");
            }
        }

        return condition;
    }

    private bool TryParseTimeSpan(string value, out TimeSpan result)
    {
        var match = Regex.Match(value, @"^(\d+)(ms|s|m|h)$");
        if (!match.Success)
        {
            result = TimeSpan.Zero;
            return false;
        }

        var amount = int.Parse(match.Groups[1].Value);
        var unit = match.Groups[2].Value;

        result = unit switch
        {
            "ms" => TimeSpan.FromMilliseconds(amount),
            "s" => TimeSpan.FromSeconds(amount),
            "m" => TimeSpan.FromMinutes(amount),
            "h" => TimeSpan.FromHours(amount),
            _ => TimeSpan.Zero
        };

        return true;
    }
}
