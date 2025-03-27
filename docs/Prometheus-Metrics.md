# Prometheus Metrics Integration

## Overview

Beacon now includes Prometheus metrics integration to provide real-time visibility into runtime performance, rule execution, and Redis operations. This document describes the available metrics, how to access them, and how to set up monitoring for your Beacon applications.

## Metrics Endpoint

Beacon exposes Prometheus metrics on port 9090 by default. You can access the metrics at:

```
http://your-beacon-host:9090/metrics
```

## Available Metrics

### Cycle Timing Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `beacon_cycle_time_ms` | Gauge | Rule processing cycle time in milliseconds |
| `beacon_cycle_delay_ms` | Gauge | Delay between rule processing cycles in milliseconds |

### Rule Execution Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `beacon_rule_executions_total` | Counter | Total number of rule executions (labeled by rule name and result) |
| `beacon_rule_execution_duration_seconds` | Histogram | Duration of rule executions in seconds (labeled by rule name) |
| `beacon_output_events_total` | Counter | Total number of output events produced (labeled by output key) |

### Redis Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `beacon_redis_connections_active` | Gauge | Number of active Redis connections |

## Example Prometheus Configuration

Here's an example `prometheus.yml` configuration that you can use to scrape metrics from Beacon:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'beacon'
    static_configs:
      - targets: ['beacon-host:9090']
```

## Grafana Dashboard

You can create a Grafana dashboard to visualize Beacon metrics. Here's a sample dashboard JSON that you can import:

```json
{
  "annotations": {
    "list": [
      {
        "builtIn": 1,
        "datasource": "-- Grafana --",
        "enable": true,
        "hide": true,
        "iconColor": "rgba(0, 211, 255, 1)",
        "name": "Annotations & Alerts",
        "type": "dashboard"
      }
    ]
  },
  "editable": true,
  "gnetId": null,
  "graphTooltip": 0,
  "id": 1,
  "links": [],
  "panels": [
    {
      "aliasColors": {},
      "bars": false,
      "dashLength": 10,
      "dashes": false,
      "datasource": null,
      "fieldConfig": {
        "defaults": {
          "custom": {}
        },
        "overrides": []
      },
      "fill": 1,
      "fillGradient": 0,
      "gridPos": {
        "h": 8,
        "w": 12,
        "x": 0,
        "y": 0
      },
      "hiddenSeries": false,
      "id": 2,
      "legend": {
        "avg": false,
        "current": false,
        "max": false,
        "min": false,
        "show": true,
        "total": false,
        "values": false
      },
      "lines": true,
      "linewidth": 1,
      "nullPointMode": "null",
      "options": {
        "alertThreshold": true
      },
      "percentage": false,
      "pluginVersion": "7.3.6",
      "pointradius": 2,
      "points": false,
      "renderer": "flot",
      "seriesOverrides": [],
      "spaceLength": 10,
      "stack": false,
      "steppedLine": false,
      "targets": [
        {
          "expr": "beacon_cycle_time_ms",
          "interval": "",
          "legendFormat": "{{instance}}",
          "refId": "A"
        }
      ],
      "thresholds": [],
      "timeFrom": null,
      "timeRegions": [],
      "timeShift": null,
      "title": "Cycle Time (ms)",
      "tooltip": {
        "shared": true,
        "sort": 0,
        "value_type": "individual"
      },
      "type": "graph",
      "xaxis": {
        "buckets": null,
        "mode": "time",
        "name": null,
        "show": true,
        "values": []
      },
      "yaxes": [
        {
          "format": "ms",
          "label": null,
          "logBase": 1,
          "max": null,
          "min": null,
          "show": true
        },
        {
          "format": "short",
          "label": null,
          "logBase": 1,
          "max": null,
          "min": null,
          "show": true
        }
      ],
      "yaxis": {
        "align": false,
        "alignLevel": null
      }
    },
    {
      "aliasColors": {},
      "bars": false,
      "dashLength": 10,
      "dashes": false,
      "datasource": null,
      "fieldConfig": {
        "defaults": {
          "custom": {}
        },
        "overrides": []
      },
      "fill": 1,
      "fillGradient": 0,
      "gridPos": {
        "h": 8,
        "w": 12,
        "x": 12,
        "y": 0
      },
      "hiddenSeries": false,
      "id": 4,
      "legend": {
        "avg": false,
        "current": false,
        "max": false,
        "min": false,
        "show": true,
        "total": false,
        "values": false
      },
      "lines": true,
      "linewidth": 1,
      "nullPointMode": "null",
      "options": {
        "alertThreshold": true
      },
      "percentage": false,
      "pluginVersion": "7.3.6",
      "pointradius": 2,
      "points": false,
      "renderer": "flot",
      "seriesOverrides": [],
      "spaceLength": 10,
      "stack": false,
      "steppedLine": false,
      "targets": [
        {
          "expr": "rate(beacon_rule_executions_total[1m])",
          "interval": "",
          "legendFormat": "{{rule_name}}",
          "refId": "A"
        }
      ],
      "thresholds": [],
      "timeFrom": null,
      "timeRegions": [],
      "timeShift": null,
      "title": "Rule Executions Rate",
      "tooltip": {
        "shared": true,
        "sort": 0,
        "value_type": "individual"
      },
      "type": "graph",
      "xaxis": {
        "buckets": null,
        "mode": "time",
        "name": null,
        "show": true,
        "values": []
      },
      "yaxes": [
        {
          "format": "short",
          "label": null,
          "logBase": 1,
          "max": null,
          "min": null,
          "show": true
        },
        {
          "format": "short",
          "label": null,
          "logBase": 1,
          "max": null,
          "min": null,
          "show": true
        }
      ],
      "yaxis": {
        "align": false,
        "alignLevel": null
      }
    }
  ],
  "refresh": "5s",
  "schemaVersion": 26,
  "style": "dark",
  "tags": [],
  "templating": {
    "list": []
  },
  "time": {
    "from": "now-5m",
    "to": "now"
  },
  "timepicker": {},
  "timezone": "",
  "title": "Beacon Dashboard",
  "uid": "beacon",
  "version": 1
}
```

## Prometheus and Grafana Setup

To set up a complete monitoring stack for Beacon:

1. Install Prometheus:
   ```bash
   # Download Prometheus
   wget https://github.com/prometheus/prometheus/releases/download/v2.45.0/prometheus-2.45.0.linux-amd64.tar.gz
   
   # Extract
   tar xvfz prometheus-2.45.0.linux-amd64.tar.gz
   cd prometheus-2.45.0.linux-amd64/
   
   # Configure
   cat > prometheus.yml << EOF
   global:
     scrape_interval: 15s
   
   scrape_configs:
     - job_name: 'beacon'
       static_configs:
         - targets: ['localhost:9090']
   EOF
   
   # Run Prometheus
   ./prometheus --config.file=prometheus.yml
   ```

2. Install Grafana:
   ```bash
   # Add Grafana repository
   sudo apt-get install -y apt-transport-https software-properties-common
   sudo add-apt-repository "deb https://packages.grafana.com/oss/deb stable main"
   wget -q -O - https://packages.grafana.com/gpg.key | sudo apt-key add -
   
   # Install Grafana
   sudo apt-get update
   sudo apt-get install grafana
   
   # Start Grafana
   sudo systemctl start grafana-server
   sudo systemctl enable grafana-server
   ```

3. Configure Grafana:
   - Access Grafana at http://localhost:3000 (default username/password: admin/admin)
   - Add Prometheus as a data source
   - Import the dashboard JSON above

## Alerting on Metrics

You can set up alerts in Prometheus using alerting rules. Here's an example `alert.rules.yml` file:

```yaml
groups:
- name: beacon
  rules:
  - alert: BeaconHighCycleTime
    expr: beacon_cycle_time_ms > 80
    for: 1m
    labels:
      severity: warning
    annotations:
      summary: "Beacon cycle time high"
      description: "Beacon cycle time is above 80ms for instance {{ $labels.instance }}"

  - alert: BeaconRuleExecutionFailures
    expr: increase(beacon_rule_executions_total{result="failure"}[5m]) > 0
    for: 1m
    labels:
      severity: warning
    annotations:
      summary: "Beacon rule execution failures"
      description: "Beacon has rule execution failures for rule {{ $labels.rule_name }}"
```

## Adding Custom Metrics

The `MetricsService` class provides a simple interface for adding custom metrics to your Beacon application. You can extend it as needed:

```csharp
// Adding a custom Gauge metric
var myGauge = Metrics.CreateGauge("custom_beacon_value", "Description of the value");
myGauge.Set(42.0);

// Adding a custom Counter
var myCounter = Metrics.CreateCounter("custom_beacon_events", "Count of events");
myCounter.Inc();

// Adding a custom Histogram
var myHistogram = Metrics.CreateHistogram("custom_beacon_operation_duration", "Duration of operation");
using (myHistogram.NewTimer())
{
    // Code to measure
}
```

## Correlating Logs with Metrics

For easier troubleshooting, you can correlate logs with metrics by:

1. Adding trace IDs to log messages
2. Including the same trace ID as a label in metrics
3. Using exemplars in Prometheus to link metrics with traces

This enables you to quickly jump from a metric anomaly to the relevant logs for investigation.

## Performance Considerations

The Prometheus metrics collection adds minimal overhead to your Beacon application. However, when running in high-throughput environments with many rules, consider:

1. Increasing the scrape interval to reduce collection frequency
2. Adjusting histogram buckets for better accuracy at your specific scale
3. Monitoring the metrics collection process itself to ensure it doesn't impact rule execution performance