using Pulsar.RuleDefinition.Models;

namespace Pulsar.Compiler.Models;

/// <summary>
/// Represents a rule that has been compiled and analyzed for dependencies
/// </summary>
public class CompiledRule
{
    /// <summary>
    /// The original rule definition
    /// </summary>
    public Rule Rule { get; }

    /// <summary>
    /// The layer this rule belongs to (0-based index)
    /// </summary>
    public int Layer { get; }

    /// <summary>
    /// Names of rules that this rule depends on
    /// </summary>
    public IReadOnlySet<string> Dependencies { get; }

    /// <summary>
    /// Names of sensors that this rule reads from
    /// </summary>
    public IReadOnlySet<string> InputSensors { get; }

    /// <summary>
    /// Names of sensors that this rule writes to
    /// </summary>
    public IReadOnlySet<string> OutputSensors { get; }

    public CompiledRule(
        Rule rule,
        int layer,
        IReadOnlySet<string> dependencies,
        IReadOnlySet<string> inputSensors,
        IReadOnlySet<string> outputSensors)
    {
        Rule = rule;
        Layer = layer;
        Dependencies = dependencies;
        InputSensors = inputSensors;
        OutputSensors = outputSensors;
    }
}
