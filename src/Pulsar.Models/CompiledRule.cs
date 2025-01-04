using System.Collections.Generic;
using Pulsar.RuleDefinition.Models;

namespace Pulsar.Models;

/// <summary>
/// Represents a compiled rule with its layer information and dependencies
/// </summary>
public class CompiledRule
{
    /// <summary>
    /// The original rule definition
    /// </summary>
    public RuleDefinitionModel RuleDefinition { get; }

    /// <summary>
    /// The layer this rule belongs to in the execution order
    /// </summary>
    public int Layer { get; }

    /// <summary>
    /// The rules that this rule depends on
    /// </summary>
    public IReadOnlyCollection<string> Dependencies { get; }

    /// <summary>
    /// Input sensors this rule reads from
    /// </summary>
    public ISet<string> InputSensors { get; }

    /// <summary>
    /// Output sensors this rule writes to
    /// </summary>
    public ISet<string> OutputSensors { get; }

    /// <summary>
    /// Creates a new CompiledRule
    /// </summary>
    /// <param name="ruleDefinition">The original rule definition</param>
    /// <param name="layer">The layer this rule belongs to</param>
    /// <param name="dependencies">The rules that this rule depends on</param>
    public CompiledRule(RuleDefinitionModel ruleDefinition, int layer, IReadOnlyCollection<string> dependencies)
    {
        RuleDefinition = ruleDefinition;
        Layer = layer;
        Dependencies = dependencies;
    }
}
