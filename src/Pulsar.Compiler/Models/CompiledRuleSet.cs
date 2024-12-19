namespace Pulsar.Compiler.Models;

/// <summary>
/// Represents a complete set of compiled rules with their dependencies analyzed
/// </summary>
public class CompiledRuleSet
{
    /// <summary>
    /// All compiled rules, ordered by layer
    /// </summary>
    public IReadOnlyList<CompiledRule> Rules { get; }

    /// <summary>
    /// Number of execution layers
    /// </summary>
    public int LayerCount { get; }

    /// <summary>
    /// All input sensors used by any rule
    /// </summary>
    public IReadOnlySet<string> AllInputSensors { get; }

    /// <summary>
    /// All output sensors modified by any rule
    /// </summary>
    public IReadOnlySet<string> AllOutputSensors { get; }

    public CompiledRuleSet(
        IReadOnlyList<CompiledRule> rules,
        int layerCount,
        IReadOnlySet<string> allInputSensors,
        IReadOnlySet<string> allOutputSensors)
    {
        Rules = rules;
        LayerCount = layerCount;
        AllInputSensors = allInputSensors;
        AllOutputSensors = allOutputSensors;
    }
}
