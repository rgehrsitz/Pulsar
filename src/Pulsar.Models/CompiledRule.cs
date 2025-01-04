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

    /// <summary>
    /// Creates a new CompiledRule
    /// </summary>
    /// <param name="ruleDefinition">The original rule definition</param>
    /// <param name="layer">The layer this rule belongs to</param>
    /// <param name="dependencies">Names of rules this rule depends on</param>
    /// <param name="inputSensors">Input sensors this rule reads from</param>
    /// <param name="outputSensors">Output sensors this rule writes to</param>
    public CompiledRule(
        RuleDefinitionModel ruleDefinition,
        int layer,
        ISet<string> dependencies,
        ISet<string> inputSensors,
        ISet<string> outputSensors
    )
    {
        RuleDefinition = ruleDefinition;
        Layer = layer;
        Dependencies = dependencies;
        InputSensors = inputSensors;
        OutputSensors = outputSensors;
    }
}
