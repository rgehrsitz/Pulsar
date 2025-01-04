using System.Collections.Generic;
using Pulsar.RuleDefinition.Models.Conditions;

namespace Pulsar.RuleDefinition.Models;

public class ConditionGroupDefinition
{
    public List<ConditionDefinition>? All { get; set; }
    public List<ConditionDefinition>? Any { get; set; }
}

public class ConditionDefinition
{
    public Conditions.Condition Condition { get; set; } = null!;
}
