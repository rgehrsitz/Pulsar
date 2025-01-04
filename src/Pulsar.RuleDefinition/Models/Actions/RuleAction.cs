namespace Pulsar.RuleDefinition.Models.Actions;

public class RuleAction
{
    public SetValueAction? SetValue { get; set; }
    public SendMessageAction? SendMessage { get; set; }
}

public class SetValueAction
{
    public string Key { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string? ValueExpression { get; set; }
}

public class SendMessageAction
{
    public string Channel { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
