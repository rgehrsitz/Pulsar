using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Pulsar.RuleDefinition.Models;

/// <summary>
/// Represents the root structure of a rules definition file
/// </summary>
public class RuleSetDefinition
{
    public int Version { get; set; }
    public List<Rule> Rules { get; set; } = new();
}

/// <summary>
/// Represents a single rule in the rule set
/// </summary>
public class Rule
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ConditionGroup Conditions { get; set; } = new();
    public List<Actions.RuleAction> Actions { get; set; } = new();
}

/// <summary>
/// Represents a group of conditions combined with a logical operator
/// </summary>
public class ConditionGroup
{
    public List<ConditionWrapper>? All { get; set; }
    public List<ConditionWrapper>? Any { get; set; }
}

/// <summary>
/// Wrapper class for conditions
/// </summary>
public class ConditionWrapper
{
    public Condition Condition { get; set; } = null!;
}

/// <summary>
/// Base class for all condition types
/// </summary>
[YamlSerializable]
public abstract class Condition
{
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Represents a simple comparison condition
/// </summary>
public class ComparisonCondition : Condition
{
    public string DataSource { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public double Value { get; set; }
}

/// <summary>
/// Represents an expression-based condition
/// </summary>
public class ExpressionCondition : Condition
{
    public string Expression { get; set; } = string.Empty;
}

/// <summary>
/// Represents an action to be taken when a rule's conditions are met
/// </summary>
public class RuleAction
{
    /// <summary>
    /// Action to set a sensor value
    /// </summary>
    public Actions.SetValueAction? SetValue { get; set; }

    /// <summary>
    /// Action to send a message
    /// </summary>
    public Actions.SendMessageAction? SendMessage { get; set; }
}
