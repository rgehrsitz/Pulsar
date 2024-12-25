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

        // Start of the mapping for the action
        parser.Consume<MappingStart>();

        // Read the action type (set_value or send_message)
        var actionType = parser.Consume<Scalar>().Value;

        // Start of the mapping for the action details
        parser.Consume<MappingStart>();

        switch (actionType)
        {
            case "set_value":
                action.SetValue = ParseSetValueAction(parser);
                break;
            case "send_message":
                action.SendMessage = ParseSendMessageAction(parser);
                break;
            default:
                // Skip unknown action types
                parser.SkipThisAndNestedEvents();
                break;
        }

        // End of the action details mapping
        parser.Consume<MappingEnd>();

        // End of the action mapping
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
                    if (double.TryParse(value, out var doubleValue))
                    {
                        action.Value = doubleValue;
                    }
                    break;
                case "value_expression":
                    action.ValueExpression = value;
                    break;
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
            }
        }

        return action;
    }
}
