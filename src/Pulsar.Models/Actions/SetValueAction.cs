using System;

namespace Pulsar.Models.Actions;

/// <summary>
/// Action that sets a sensor value
/// </summary>
public class SetValueAction
{
    /// <summary>
    /// The sensor to set
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The value to set. Can be a numeric value or a string.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// An expression that evaluates to the value to set
    /// </summary>
    public string? ValueExpression { get; set; }
}
