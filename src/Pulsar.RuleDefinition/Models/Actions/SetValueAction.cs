namespace Pulsar.RuleDefinition.Models.Actions;

/// <summary>
/// Action to set a sensor value
/// </summary>
public class SetValueAction
{
    /// <summary>
    /// The sensor key to set
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The value to set
    /// </summary>
    public double? Value { get; set; }

    /// <summary>
    /// Expression to evaluate to get the value
    /// </summary>
    public string? ValueExpression { get; set; }
}
