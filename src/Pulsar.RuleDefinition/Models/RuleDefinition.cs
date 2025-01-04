using System.Collections.Generic;
using Pulsar.RuleDefinition.Models.Actions;

namespace Pulsar.RuleDefinition.Models;

/// <summary>
/// Represents a rule definition in the DSL
/// </summary>
public class RuleDefinitionModel
{
    /// <summary>
    /// The unique name of the rule
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what the rule does
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The conditions that must be met for this rule to trigger
    /// </summary>
    public ConditionGroupDefinition Conditions { get; set; } = new();

    /// <summary>
    /// The actions to take when conditions are met
    /// </summary>
    public List<RuleAction> Actions { get; set; } = new();
}
