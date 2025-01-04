using System.Collections.Generic;
using Pulsar.RuleDefinition.Models.Conditions;

namespace Pulsar.RuleDefinition.Models;

public class ConditionGroup
{
    public List<ConditionWrapper>? All { get; set; }
    public List<ConditionWrapper>? Any { get; set; }
}

public class ConditionWrapper
{
    public Conditions.Condition Condition { get; set; } = null!;
}
