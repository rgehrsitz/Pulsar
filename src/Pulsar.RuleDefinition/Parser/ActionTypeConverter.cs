using System;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Actions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Pulsar.RuleDefinition.Parser;

public class ActionTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(RuleAction);

    public object ReadYaml(IParser parser, Type type)
    {
        var action = new RuleAction();

        parser.Consume<MappingStart>();

        // Each action should have exactly one key (set_value or send_message)
        if (parser.Current is not Scalar scalar)
        {
            throw new YamlException("Expected action type (set_value or send_message)");
        }

        parser.MoveNext();
        parser.Consume<MappingStart>();

        switch (scalar.Value)
        {
            case "set_value":
                action.SetValue = ParseSetValueAction(parser);
                break;
            case "send_message":
                action.SendMessage = ParseSendMessageAction(parser);
                break;
            default:
                throw new YamlException($"Unknown action type: {scalar.Value}");
        }

        parser.Consume<MappingEnd>();
        parser.Consume<MappingEnd>();

        return action;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        throw new NotImplementedException("Writing YAML is not supported");
    }

    private SetValueAction ParseSetValueAction(IParser parser)
    {
        var action = new SetValueAction();

        while (parser.Current is Scalar scalar)
        {
            parser.MoveNext();
            var value = parser.Consume<Scalar>().Value;

            switch (scalar.Value)
            {
                case "key":
                    action.Key = value;
                    break;
                case "value":
                    // Try parsing as number first
                    if (double.TryParse(value, out var numericValue))
                    {
                        action.Value = numericValue;
                    }
                    else
                    {
                        // If not a number, use as string
                        action.Value = value;
                    }
                    break;
                case "value_expression":
                    action.ValueExpression = value;
                    break;
                default:
                    throw new YamlException($"Unknown field in set_value action: {scalar.Value}");
            }
        }

        return action;
    }

    private SendMessageAction ParseSendMessageAction(IParser parser)
    {
        var action = new SendMessageAction();

        while (parser.Current is Scalar scalar)
        {
            parser.MoveNext();
            var value = parser.Consume<Scalar>().Value;

            switch (scalar.Value)
            {
                case "channel":
                    action.Channel = value;
                    break;
                case "message":
                    action.Message = value;
                    break;
                default:
                    throw new YamlException($"Unknown field in send_message action: {scalar.Value}");
            }
        }

        return action;
    }
}
