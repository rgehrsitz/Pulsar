using System.Collections.Generic;

namespace Pulsar.RuleDefinition.Models;

/// <summary>
/// Represents the root structure of the system configuration file
/// </summary>
public class SystemConfig
{
    public int Version { get; set; }
    public List<string> ValidSensors { get; set; } = new();
}
