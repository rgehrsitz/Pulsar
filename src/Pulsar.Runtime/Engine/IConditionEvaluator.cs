using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Conditions;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Interface for evaluating rule conditions against sensor data
/// </summary>
public interface IConditionEvaluator
{
    /// <summary>
    /// Evaluates a condition against the current sensor data
    /// </summary>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="sensorData">Current sensor data</param>
    /// <returns>True if the condition is met, false otherwise</returns>
    Task<bool> EvaluateAsync(
        ConditionDefinition condition,
        IDictionary<string, double> sensorData
    );
}
