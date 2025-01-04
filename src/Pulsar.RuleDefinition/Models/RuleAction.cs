using Pulsar.RuleDefinition.Models.Actions;

namespace Pulsar.RuleDefinition.Models;

public class RuleAction
{
    public SetValueAction? SetValue { get; set; }
    public SendMessageAction? SendMessage { get; set; }
}
