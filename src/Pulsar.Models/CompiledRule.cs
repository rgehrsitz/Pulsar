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
    public Rule Rule { get; }

    /// <summary>
    /// The layer this rule belongs to in the execution order
    /// </summary>
    public int Layer { get; }

    /// <summary>
    /// Names of rules this rule depends on
    /// </summary>
    public ISet<string> Dependencies { get; }

    /// <summary>
    /// Input sensors this rule reads from
    /// </summary>
    public ISet<string> InputSensors { get; }

    /// <summary>
    /// Output sensors this rule writes to
    /// </summary>
    public ISet<string> OutputSensors { get; }

    public CompiledRule(
        Rule rule,
        int layer,
        ISet<string> dependencies,
        ISet<string> inputSensors,
        ISet<string> outputSensors
    )
    {
        Rule = rule;
        Layer = layer;
        Dependencies = dependencies;
        InputSensors = inputSensors;
        OutputSensors = outputSensors;
    }
}
