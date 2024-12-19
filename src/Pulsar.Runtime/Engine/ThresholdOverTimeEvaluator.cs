using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Pulsar.Core;
using Pulsar.Runtime.Services;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Validation;
using Serilog;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Evaluates threshold over time conditions against sensor data
/// </summary>
public class ThresholdOverTimeEvaluator : IConditionEvaluator
{
    private readonly ISensorDataProvider _dataProvider;
    private readonly TemporalValidator _validator;
    private readonly ILogger _logger;
    private readonly TimeSeriesService _timeSeriesService;
    private readonly TimeSpan _samplingRate;
    private readonly IMetricsService _metricsService;

    public ThresholdOverTimeEvaluator(
        ISensorDataProvider dataProvider,
        ILogger logger,
        IMetricsService metricsService,
        TimeSpan? samplingRate = null)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _logger = logger.ForContext<ThresholdOverTimeEvaluator>();
        _validator = new TemporalValidator();
        _metricsService = metricsService;
        _timeSeriesService = new TimeSeriesService(logger, metricsService, 100); // Store last 100 values per sensor
        _samplingRate = samplingRate ?? TimeSpan.FromSeconds(1);
    }

    public async Task<bool> EvaluateAsync(Condition condition, IDictionary<string, double> sensorData)
    {
        if (condition is not ThresholdOverTimeCondition thresholdCondition)
        {
            _logger.Warning("Invalid condition type {ConditionType}", condition.GetType().Name);
            return false;
        }

        var errors = _validator.ValidateTemporalCondition(thresholdCondition);
        if (errors.Any())
        {
            _logger.Warning("Invalid condition: {ValidationErrors}", string.Join(", ", errors));
            return false;
        }

        var dataSource = thresholdCondition.DataSource;
        if (!sensorData.TryGetValue(dataSource, out var currentValue))
        {
            _logger.Warning("No data found for source {DataSource}", dataSource);
            return false;
        }

        var now = DateTime.UtcNow;
        var lastTimestamp = _timeSeriesService.GetTimeWindow(dataSource, TimeSpan.FromMilliseconds(1)).LastOrDefault().Timestamp;

        _logger.Debug("Current value for {DataSource}: {Value} at {Timestamp}", 
            dataSource, currentValue, now);

        // Only add the value if enough time has passed since the last update
        if (lastTimestamp == default || now - lastTimestamp >= _samplingRate)
        {
            _logger.Debug("Updating time series for {DataSource}. Last update was {LastUpdate}", 
                dataSource, lastTimestamp == default ? "never" : lastTimestamp.ToString());
            _timeSeriesService.Update(dataSource, currentValue);
        }
        else
        {
            _logger.Debug("Skipping update for {DataSource} - Not enough time elapsed since last update ({LastUpdate})", 
                dataSource, lastTimestamp);
        }

        var duration = ParseDuration(thresholdCondition.Duration);
        var values = _timeSeriesService.GetTimeWindow(dataSource, duration);

        if (!values.Any())
        {
            _logger.Warning("No values found in time window for source {DataSource}", dataSource);
            return false;
        }

        var result = thresholdCondition.Operator switch
        {
            ThresholdOperator.GreaterThan => values.All(v => v.Value > thresholdCondition.Threshold),
            ThresholdOperator.LessThan => values.All(v => v.Value < thresholdCondition.Threshold),
            _ => throw new ArgumentException($"Invalid operator {thresholdCondition.Operator}")
        };

        _metricsService.RecordConditionEvaluation("", "ThresholdOverTime", result);
        return result;
    }

    private static TimeSpan ParseDuration(string duration)
    {
        if (string.IsNullOrEmpty(duration))
        {
            throw new ArgumentException("Duration cannot be null or empty", nameof(duration));
        }

        var lastChar = duration[^1];
        var value = int.Parse(duration[..^1]);

        return lastChar switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            'd' => TimeSpan.FromDays(value),
            _ => throw new ArgumentException($"Invalid duration format: {duration}. Use s, m, h, or d suffix.")
        };
    }
}
