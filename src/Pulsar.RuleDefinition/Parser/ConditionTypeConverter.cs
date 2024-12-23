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
        // Start of the mapping
        parser.Consume<MappingStart>();

        // Read the condition type
        parser.Consume<Scalar>(); // "type" key
        var conditionType = parser.Consume<Scalar>().Value;

        // Create the appropriate condition type
        Condition condition = conditionType switch
        {
            "comparison" => new ComparisonCondition(),
            "threshold_over_time" => new ThresholdOverTimeCondition(),
            "expression" => new ExpressionCondition(),
            _ => throw new YamlException($"Unknown condition type: {conditionType}"),
        };

        condition.Type = conditionType;

        // Parse the remaining fields based on condition type
        while (parser.Current is Scalar scalar)
        {
            parser.MoveNext();
            var value = parser.Consume<Scalar>().Value;

            switch (scalar.Value)
            {
                case "data_source" when condition is ComparisonCondition comp:
                    comp.DataSource = value;
                    break;
                case "operator" when condition is ComparisonCondition comp:
                    comp.Operator = value;
                    break;
                case "value" when condition is ComparisonCondition comp:
                    comp.Value = double.Parse(value);
                    break;
                case "data_source" when condition is ThresholdOverTimeCondition threshold:
                    threshold.DataSource = value;
                    break;
                case "threshold" when condition is ThresholdOverTimeCondition threshold:
                    threshold.Threshold = double.Parse(value);
                    break;
                case "duration" when condition is ThresholdOverTimeCondition threshold:
                    threshold.Duration = value;
                    break;
                case "operator" when condition is ThresholdOverTimeCondition threshold:
                    threshold.Operator = value.ToLowerInvariant() switch
                    {
                        ">" or "gt" => ThresholdOperator.GreaterThan,
                        "<" or "lt" => ThresholdOperator.LessThan,
                        _ => throw new YamlException($"Invalid threshold operator: {value}"),
                    };
                    break;
                case "required_percentage" when condition is ThresholdOverTimeCondition threshold:
                    if (value.EndsWith("%"))
                    {
                        value = value[..^1];
                    }
                    threshold.RequiredPercentage = double.Parse(value) / 100.0;
                    break;
                case "expression" when condition is ExpressionCondition expr:
                    expr.Expression = value;
                    break;
            }
        }

        // End of the mapping
        parser.Consume<MappingEnd>();

        return condition;
    }

    public void WriteYaml(IEmitter emitter, object value, Type type)
    {
        var condition = (Condition)value;

        emitter.Emit(new MappingStart());

        // Write type
        emitter.Emit(new Scalar("type"));
        emitter.Emit(new Scalar(condition.Type));

        // Write specific fields based on condition type
        switch (condition)
        {
            case ComparisonCondition comp:
                emitter.Emit(new Scalar("data_source"));
                emitter.Emit(new Scalar(comp.DataSource));
                emitter.Emit(new Scalar("operator"));
                emitter.Emit(new Scalar(comp.Operator));
                emitter.Emit(new Scalar("value"));
                emitter.Emit(new Scalar(comp.Value.ToString()));
                break;
            case ThresholdOverTimeCondition threshold:
                emitter.Emit(new Scalar("data_source"));
                emitter.Emit(new Scalar(threshold.DataSource));
                emitter.Emit(new Scalar("threshold"));
                emitter.Emit(new Scalar(threshold.Threshold.ToString()));
                emitter.Emit(new Scalar("duration"));
                emitter.Emit(new Scalar(threshold.Duration));
                emitter.Emit(new Scalar("operator"));
                emitter.Emit(
                    new Scalar(
                        threshold.Operator switch
                        {
                            ThresholdOperator.GreaterThan => ">",
                            ThresholdOperator.LessThan => "<",
                            _ => throw new YamlException(
                                $"Invalid threshold operator: {threshold.Operator}"
                            ),
                        }
                    )
                );
                if (threshold.RequiredPercentage < 1.0)
                {
                    emitter.Emit(new Scalar("required_percentage"));
                    emitter.Emit(new Scalar(threshold.RequiredPercentage.ToString("P0")));
                }
                break;
            case ExpressionCondition expr:
                emitter.Emit(new Scalar("expression"));
                emitter.Emit(new Scalar(expr.Expression));
                break;
        }

        emitter.Emit(new MappingEnd());
    }
}
