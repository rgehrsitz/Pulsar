using System;
using Pulsar.Core.Services;

namespace Pulsar.Runtime.Services;

/// <summary>
/// Interface for recording metrics using Prometheus
/// </summary>
public interface IMetricsService : Core.Services.IMetricsService
{
    /// <summary>
    /// Records a condition evaluation
    /// </summary>
    void RecordConditionEvaluation(string ruleName, string conditionType, bool result);

    /// <summary>
    /// Records a rule execution
    /// </summary>
    void RecordRuleExecution(string ruleName);

    /// <summary>
    /// Records a rule execution error
    /// </summary>
    void RecordRuleExecutionError(string ruleName, string errorType);

    /// <summary>
    /// Measures the duration of a rule execution
    /// </summary>
    IDisposable MeasureRuleExecutionDuration(string ruleName);

    /// <summary>
    /// Records an action execution
    /// </summary>
    void RecordActionExecution(string ruleName, string actionType);

    /// <summary>
    /// Records an action execution error
    /// </summary>
    void RecordActionExecutionError(string ruleName, string actionType, string errorType);

    /// <summary>
    /// Records a node status update
    /// </summary>
    void RecordNodeStatus(string nodeType, string endpoint, bool isConnected, string buildingId);

    /// <summary>
    /// Records Pulsar instance status
    /// </summary>
    void RecordPulsarStatus(string buildingId, bool isActive);
}
