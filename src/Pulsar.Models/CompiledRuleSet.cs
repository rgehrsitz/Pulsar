using System.Collections.Generic;

namespace Pulsar.Models;

/// <summary>
/// Represents a set of compiled rules organized into layers
/// </summary>
public class CompiledRuleSet
{
    /// <summary>
    /// List of all compiled rules
    /// </summary>
    public IReadOnlyList<CompiledRule> Rules { get; }

    /// <summary>
    /// Number of layers in the rule set
    /// </summary>
    public int LayerCount { get; }

    /// <summary>
    /// All input sensors used by rules in this set
    /// </summary>
    public ISet<string> AllInputSensors { get; }

    /// <summary>
    /// All output sensors written to by rules in this set
    /// </summary>
    public ISet<string> AllOutputSensors { get; }

    public CompiledRuleSet(
        IReadOnlyList<CompiledRule> rules,
        int layerCount,
        ISet<string> allInputSensors,
        ISet<string> allOutputSensors
    )
    {
        Rules = rules;
        LayerCount = layerCount;
        AllInputSensors = allInputSensors;
        AllOutputSensors = allOutputSensors;
    }
}
