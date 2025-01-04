namespace Pulsar.RuleDefinition.Models.Conditions;

/// <summary>
/// Represents an expression-based condition
/// </summary>
public class ExpressionCondition : Condition
{
    public string Expression { get; set; } = string.Empty;
}
