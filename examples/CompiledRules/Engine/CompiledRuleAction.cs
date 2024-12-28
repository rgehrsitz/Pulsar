namespace Pulsar.CompiledRules
{
    public class CompiledRuleAction
    {
        public SetValueAction? SetValue { get; set; }
        public SendMessageAction? SendMessage { get; set; }
    }

    public class SetValueAction
    {
        public string Key { get; set; } = "";
        public double Value { get; set; }
    }

    public class SendMessageAction
    {
        public string Channel { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
