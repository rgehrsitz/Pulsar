using System;
using System.Text.RegularExpressions;
using Pulsar.RuleDefinition.Models;
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

    private ComparisonCondition ParseComparisonCondition(IParser parser)
    {
        var condition = new ComparisonCondition();

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

    private ThresholdOverTimeCondition ParseThresholdOverTimeCondition(IParser parser)
    {
        var condition = new ThresholdOverTimeCondition();

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
                    if (double.TryParse(value, out var threshold))
                    {
                        condition.Threshold = threshold;
                    }
                    else
                    {
                        throw new YamlException($"Invalid threshold value: {value}");
                    }
                    break;
                case "duration":
                    var (isValid, durationMs, error) = TryParseTimeSpan(value);
                    if (!isValid)
                    {
                        throw new YamlException($"Invalid duration: {error}");
                    }
                    condition.DurationMs = durationMs;
                    break;
                default:
                    throw new YamlException($"Unknown field: {scalar.Value}");
            }
        }

        return condition;
    }

    private ExpressionCondition ParseExpressionCondition(IParser parser)
    {
        var condition = new ExpressionCondition();

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

    private (bool isValid, int durationMs, string? error) TryParseTimeSpan(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (false, 0, "Duration cannot be empty");
        }

        // Parse duration with unit
        var match = Regex.Match(value, @"^(\d+)(ms|s|m|h|d)$");
        if (!match.Success)
        {
            return (false, 0, "Duration must be specified with a valid unit (ms, s, m, h, d)");
        }

        if (!int.TryParse(match.Groups[1].Value, out int numericValue))
        {
            return (false, 0, "Invalid duration value");
        }

        var unit = match.Groups[2].Value;
        var multiplier = unit switch
        {
            "ms" => 1,
            "s" => 1000,
            "m" => 60 * 1000,
            "h" => 60 * 60 * 1000,
            "d" => 24 * 60 * 60 * 1000,
            _ => throw new ArgumentException($"Invalid duration unit: {unit}")
        };

        var totalMs = numericValue * multiplier;
        return (true, totalMs, null);
    }
}
