using System;

namespace Pulsar.Runtime.Services;

/// <summary>
/// Interface for collecting and exposing metrics
/// </summary>
public interface IMetricsService
{
    void RecordConditionEvaluation(string ruleName, string conditionType, bool result);
    void RecordRuleExecution(string ruleName);
    void RecordRuleExecutionError(string ruleName, string errorType);
    IDisposable MeasureRuleExecutionDuration(string ruleName);
    void RecordActionExecution(string ruleName, string actionType);
    void RecordActionExecutionError(string ruleName, string actionType, string errorType);
    void UpdateTimeSeriesBufferSize(string dataSource, double size);
    void RecordTimeSeriesUpdate(string dataSource);
    void RecordTimeSeriesOverflow(string dataSource);
    void UpdateSensorValue(string sensorName, double value);
    void RecordSensorUpdate(string sensorName);
    void RecordSensorReadError(string sensorName, string errorType);
    void RecordThresholdEvaluation(string sensor, bool result, int durationMs);
}
