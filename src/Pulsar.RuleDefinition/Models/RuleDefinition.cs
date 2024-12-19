using System.Collections.Generic;

namespace Pulsar.RuleDefinition.Models;

/// <summary>
/// Represents the root structure of a rules definition file
/// </summary>
public class RuleSetDefinition
{
    public int Version { get; set; }
    public List<string> ValidDataSources { get; set; } = new();
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
/// Represents a threshold over time condition
/// </summary>
public class ThresholdOverTimeCondition : Condition
{
    public string DataSource { get; set; } = string.Empty;
    public double Threshold { get; set; }
    public string Duration { get; set; } = string.Empty;
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
    public Dictionary<string, object> SetValue { get; set; } = new();
    public Dictionary<string, string> SendMessage { get; set; } = new();
}
