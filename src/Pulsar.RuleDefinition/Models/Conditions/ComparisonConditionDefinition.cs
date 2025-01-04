namespace Pulsar.RuleDefinition.Models.Conditions;

/// <summary>
/// Represents a simple comparison condition
/// </summary>
public class ComparisonConditionDefinition : Condition
{
    public string DataSource { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
