namespace Pulsar.RuleDefinition.Models.Conditions;

/// <summary>
/// Represents a simple comparison condition
/// </summary>
public class ComparisonCondition : Condition
{
    public string DataSource { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public double Value { get; set; }
}
