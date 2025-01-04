using System.Collections.Generic;

namespace Pulsar.RuleDefinition.Models;

/// <summary>
/// Represents the root structure of a rules definition file
/// </summary>
public class RuleSetDefinition
{
    public int Version { get; set; }
    public List<Rule> Rules { get; set; } = new();
}
