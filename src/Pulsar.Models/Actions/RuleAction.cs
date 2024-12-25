namespace Pulsar.Models.Actions;

/// <summary>
/// Represents a compiled action to be taken when a rule's conditions are met
/// </summary>
public class CompiledRuleAction
{
    /// <summary>
    /// Action to set a sensor value
    /// </summary>
    public SetValueAction? SetValue { get; set; }

    /// <summary>
    /// Action to send a message
    /// </summary>
    public SendMessageAction? SendMessage { get; set; }
}
