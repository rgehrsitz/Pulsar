// File: Pulsar.Compiler/Config/Templates/Runtime/Services/MetricsService.cs
// Version: 1.0.0

using System;
using System.Collections.Generic;
using Prometheus;
using Serilog;

namespace Beacon.Runtime.Services
{
    public class MetricsService
    {
        private readonly ILogger _logger;
        private readonly string _instanceName;
        private static readonly Counter RuleExecutionsTotal = Metrics.CreateCounter(
            "beacon_rule_executions_total", 
            "Total number of rule executions",
            new string[] { "instance", "rule_name", "result" });
        
        private static readonly Counter OutputEventsTotal = Metrics.CreateCounter(
            "beacon_output_events_total", 
            "Total number of output events",
            new string[] { "instance", "output_key" });
        
        private static readonly Gauge CycleTime = Metrics.CreateGauge(
            "beacon_cycle_time_ms", 
            "Rule processing cycle time in milliseconds",
            new string[] { "instance" });
        
        private static readonly Gauge CycleDelay = Metrics.CreateGauge(
            "beacon_cycle_delay_ms", 
            "Delay between rule processing cycles in milliseconds",
            new string[] { "instance" });
        
        private static readonly Histogram RuleExecutionDuration = Metrics.CreateHistogram(
            "beacon_rule_execution_duration_seconds",
            "Duration of rule execution in seconds",
            new HistogramConfiguration 
            {
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 10), // Start at 1ms, double 10 times
                LabelNames = new[] { "instance", "rule_name" }
            });

        private static readonly Gauge ActiveConnections = Metrics.CreateGauge(
            "beacon_redis_connections_active", 
            "Number of active Redis connections",
            new string[] { "instance" });

        public MetricsService(ILogger logger, string instanceName = "default")
        {
            _logger = logger.ForContext<MetricsService>();
            _instanceName = instanceName;
            _logger.Information("Metrics service initialized for instance '{InstanceName}'", _instanceName);
        }

        public void RecordRuleExecution(string ruleName, bool success)
        {
            var result = success ? "success" : "failure";
            RuleExecutionsTotal.WithLabels(_instanceName, ruleName, result).Inc();
        }

        public void RecordOutputEvent(string outputKey)
        {
            OutputEventsTotal.WithLabels(_instanceName, outputKey).Inc();
        }

        public void RecordOutputEvents(Dictionary<string, object> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return;

            foreach (var key in outputs.Keys)
            {
                RecordOutputEvent(key);
            }
        }

        public void RecordCycleTiming(int elapsedMs, int delayMs)
        {
            CycleTime.WithLabels(_instanceName).Set(elapsedMs);
            CycleDelay.WithLabels(_instanceName).Set(delayMs);
        }

        public IDisposable MeasureRuleExecutionTime(string ruleName)
        {
            return RuleExecutionDuration.WithLabels(_instanceName, ruleName).NewTimer();
        }

        public void RecordRedisConnections(int count)
        {
            ActiveConnections.WithLabels(_instanceName).Set(count);
        }

        public void StartMetricsServer(int port = 9090)
        {
            var server = new KestrelMetricServer(port: port);
            server.Start();
            _logger.Information("Prometheus metrics server started on port {Port}", port);
        }
    }
}