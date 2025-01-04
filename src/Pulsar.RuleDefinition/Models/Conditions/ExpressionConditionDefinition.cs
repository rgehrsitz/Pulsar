namespace Pulsar.RuleDefinition.Models.Conditions;

/// <summary>
/// Represents an expression-based condition
/// </summary>
public class ExpressionConditionDefinition : Condition
{
    public string Expression { get; set; } = string.Empty;
}
