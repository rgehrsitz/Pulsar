using System.Collections.Generic;

namespace Pulsar.RuleDefinition.Models;

public class ConditionGroup
{
    public List<ConditionWrapper>? All { get; set; }
    public List<ConditionWrapper>? Any { get; set; }
}

public class ConditionWrapper
{
    public Condition Condition { get; set; } = null!;
}
