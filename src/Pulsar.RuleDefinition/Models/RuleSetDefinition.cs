using System.Collections.Generic;

namespace Pulsar.RuleDefinition.Models;

/// <summary>
/// Represents the root structure of a rules definition file
/// </summary>
public class RuleSetDefinition
{
    public int Version { get; set; }
    /// <summary>
    /// The list of rules in this set
    /// </summary>
    public List<RuleDefinitionModel> Rules { get; set; } = new();
}
