using System.Collections.Generic;
using Pulsar.RuleDefinition.Models.Actions;

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
    public string? Description { get; set; }
    public ConditionGroup Conditions { get; set; } = new();
    public List<RuleAction> Actions { get; set; } = new();
}

/// <summary>
/// Represents a group of conditions combined with a logical operator
/// </summary>
public class ConditionGroup
{
    public List<Condition>? All { get; set; }
    public List<Condition>? Any { get; set; }
}

/// <summary>
/// Base class for all condition types
/// </summary>
public abstract class Condition
{
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
    public SetValueAction? SetValue { get; set; }

    /// <summary>
    /// Action to send a message
    /// </summary>
    public SendMessageAction? SendMessage { get; set; }
}
