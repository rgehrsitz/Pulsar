using System;
using Prometheus;
using Serilog;

namespace Pulsar.Runtime.Services;

/// <summary>
/// Service for recording metrics using Prometheus
/// </summary>
public class MetricsService : IMetricsService
{
    private readonly Counter _ruleExecutionTotal;
    private readonly Counter _ruleExecutionErrorsTotal;
    private readonly Histogram _ruleExecutionDuration;
    private readonly Counter _actionExecutionTotal;
    private readonly Counter _actionExecutionErrorsTotal;
    private readonly Counter _timeSeriesUpdatesTotal;
    private readonly Counter _timeSeriesOverflowTotal;
    private readonly Counter _sensorReadErrorsTotal;
    private readonly Gauge _timeSeriesBufferSize;
    private readonly Gauge _sensorValue;
    private readonly Counter _sensorUpdatesTotal;
    private readonly Gauge _nodeStatus;
    private readonly Gauge _pulsarStatus;
    private readonly ILogger _logger;

    public MetricsService(ILogger logger)
    {
        _logger = logger.ForContext<MetricsService>();

        _ruleExecutionTotal = Metrics.CreateCounter(
            "pulsar_rule_execution_total",
            "Total number of rule executions",
            new CounterConfiguration { LabelNames = new[] { "rule_name" } }
        );

        _ruleExecutionErrorsTotal = Metrics.CreateCounter(
            "pulsar_rule_execution_errors_total",
            "Total number of rule execution errors",
            new CounterConfiguration { LabelNames = new[] { "rule_name", "error_type" } }
        );

        _ruleExecutionDuration = Metrics.CreateHistogram(
            "pulsar_rule_execution_duration_seconds",
            "Duration of rule executions in seconds",
            new HistogramConfiguration { LabelNames = new[] { "rule_name" } }
        );

        _actionExecutionTotal = Metrics.CreateCounter(
            "pulsar_action_execution_total",
            "Total number of action executions",
            new CounterConfiguration { LabelNames = new[] { "rule_name", "action_type" } }
        );

        _actionExecutionErrorsTotal = Metrics.CreateCounter(
            "pulsar_action_execution_errors_total",
            "Total number of action execution errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "rule_name", "action_type", "error_type" },
            }
        );

        _timeSeriesUpdatesTotal = Metrics.CreateCounter(
            "pulsar_time_series_updates_total",
            "Total number of time series updates",
            new CounterConfiguration { LabelNames = new[] { "data_source" } }
        );

        _timeSeriesOverflowTotal = Metrics.CreateCounter(
            "pulsar_time_series_overflow_total",
            "Total number of time series buffer overflows",
            new CounterConfiguration { LabelNames = new[] { "data_source" } }
        );

        _sensorReadErrorsTotal = Metrics.CreateCounter(
            "pulsar_sensor_read_errors_total",
            "Total number of sensor read errors",
            new CounterConfiguration { LabelNames = new[] { "sensor_name", "error_type" } }
        );

        _timeSeriesBufferSize = Metrics.CreateGauge(
            "pulsar_time_series_buffer_size",
            "Current size of time series buffer",
            new GaugeConfiguration { LabelNames = new[] { "data_source" } }
        );

        _sensorValue = Metrics.CreateGauge(
            "pulsar_sensor_value",
            "Current value of a sensor",
            new GaugeConfiguration { LabelNames = new[] { "sensor_name" } }
        );

        _sensorUpdatesTotal = Metrics.CreateCounter(
            "pulsar_sensor_updates_total",
            "Total number of sensor updates",
            new CounterConfiguration { LabelNames = new[] { "sensor_name" } }
        );

        _nodeStatus = Metrics.CreateGauge(
            "pulsar_redis_node_status",
            "Status of Redis cluster nodes (1 = connected, 0 = disconnected)",
            new GaugeConfiguration { LabelNames = new[] { "node_type", "endpoint", "building_id" } }
        );

        _pulsarStatus = Metrics.CreateGauge(
            "pulsar_instance_status",
            "Status of Pulsar instance (1 = active, 0 = passive)",
            new GaugeConfiguration { LabelNames = new[] { "building_id" } }
        );
    }

    public void RecordConditionEvaluation(string ruleName, string conditionType, bool result)
    {
        _ruleExecutionTotal.WithLabels(ruleName).Inc();
        _logger.Debug(
            "Recorded condition evaluation: {RuleName}, {ConditionType}, {Result}",
            ruleName,
            conditionType,
            result
        );
    }

    public void RecordRuleExecution(string ruleName)
    {
        _ruleExecutionTotal.WithLabels(ruleName).Inc();
        _logger.Debug("Recorded rule execution: {RuleName}", ruleName);
    }

    public void RecordRuleExecutionError(string ruleName, string errorType)
    {
        _ruleExecutionErrorsTotal.WithLabels(ruleName, errorType).Inc();
        _logger.Debug(
            "Recorded rule execution error: {RuleName}, {ErrorType}",
            ruleName,
            errorType
        );
    }

    public IDisposable MeasureRuleExecutionDuration(string ruleName)
    {
        return _ruleExecutionDuration.WithLabels(ruleName).NewTimer();
    }

    public void RecordActionExecution(string ruleName, string actionType)
    {
        _actionExecutionTotal.WithLabels(ruleName, actionType).Inc();
        _logger.Debug("Recorded action execution: {RuleName}, {ActionType}", ruleName, actionType);
    }

    public void RecordActionExecutionError(string ruleName, string actionType, string errorType)
    {
        _actionExecutionErrorsTotal.WithLabels(ruleName, actionType, errorType).Inc();
        _logger.Debug(
            "Recorded action execution error: {RuleName}, {ActionType}, {ErrorType}",
            ruleName,
            actionType,
            errorType
        );
    }

    public void RecordTimeSeriesUpdate(string dataSource)
    {
        _timeSeriesUpdatesTotal.WithLabels(dataSource).Inc();
        _logger.Debug("Recorded time series update for {DataSource}", dataSource);
    }

    public void RecordTimeSeriesOverflow(string dataSource)
    {
        _timeSeriesOverflowTotal.WithLabels(dataSource).Inc();
        _logger.Debug("Recorded time series overflow for {DataSource}", dataSource);
    }

    public void RecordSensorReadError(string sensorName, string errorType)
    {
        _sensorReadErrorsTotal.WithLabels(sensorName, errorType).Inc();
        _logger.Debug(
            "Recorded sensor read error: {SensorName}, {ErrorType}",
            sensorName,
            errorType
        );
    }

    public void UpdateTimeSeriesBufferSize(string dataSource, double size)
    {
        _timeSeriesBufferSize.WithLabels(dataSource).Set(size);
        _logger.Debug("Updated time series buffer size for {DataSource}: {Size}", dataSource, size);
    }

    public void UpdateSensorValue(string sensorName, double value)
    {
        _sensorValue.WithLabels(sensorName).Set(value);
        _logger.Debug("Updated sensor value for {SensorName}: {Value}", sensorName, value);
    }

    public void RecordSensorUpdate(string sensorName)
    {
        _sensorUpdatesTotal.WithLabels(sensorName).Inc();
        _logger.Debug("Recorded sensor update for {SensorName}", sensorName);
    }

    public void RecordNodeStatus(
        string nodeType,
        string endpoint,
        bool isConnected,
        string buildingId
    )
    {
        _nodeStatus.WithLabels(nodeType, endpoint, buildingId).Set(isConnected ? 1 : 0);
        _logger.Debug(
            "Recorded node status: {NodeType} {Endpoint} in Building {BuildingId} = {Status}",
            nodeType,
            endpoint,
            buildingId,
            isConnected ? "Connected" : "Disconnected"
        );
    }

    public void RecordPulsarStatus(string buildingId, bool isActive)
    {
        _pulsarStatus.WithLabels(buildingId).Set(isActive ? 1 : 0);
        _logger.Debug(
            "Recorded Pulsar status in Building {BuildingId} = {Status}",
            buildingId,
            isActive ? "Active" : "Passive"
        );
    }
}
