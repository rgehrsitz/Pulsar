using System.Collections.Generic;

namespace Pulsar.RuleDefinition.Models;

public class Rule
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ConditionGroup Conditions { get; set; } = new();
    public List<RuleAction> Actions { get; set; } = new();
}
