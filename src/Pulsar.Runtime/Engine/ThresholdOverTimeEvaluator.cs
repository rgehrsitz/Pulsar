using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Core;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Validation;
using Pulsar.Runtime.Services;
using Pulsar.Core.Services;
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
        TimeSpan? samplingRate = null
    )
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _logger = logger.ForContext<ThresholdOverTimeEvaluator>();
        _validator = new TemporalValidator();
        _metricsService = metricsService;
        _timeSeriesService = new TimeSeriesService(logger, metricsService, 100); // Store last 100 values per sensor
        _samplingRate = samplingRate ?? TimeSpan.FromSeconds(1);
    }

    public async Task<bool> EvaluateAsync(
        Condition condition,
        IDictionary<string, double> sensorData
    )
    {
        await Task.Yield(); // Ensure the method runs asynchronously
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
        var lastTimestamp = _timeSeriesService
            .GetTimeWindow(dataSource, TimeSpan.FromMilliseconds(1))
            .LastOrDefault()
            .Timestamp;

        _logger.Debug(
            "Current value for {DataSource}: {Value} at {Timestamp}",
            dataSource,
            currentValue,
            now
        );

        // Only add the value if enough time has passed since the last update
        if (lastTimestamp == default || now - lastTimestamp >= _samplingRate)
        {
            _logger.Debug(
                "Updating time series for {DataSource}. Last update was {LastUpdate}",
                dataSource,
                lastTimestamp == default ? "never" : lastTimestamp.ToString()
            );
            _timeSeriesService.Update(dataSource, currentValue);
        }
        else
        {
            _logger.Debug(
                "Skipping update for {DataSource} - Not enough time elapsed since last update ({LastUpdate})",
                dataSource,
                lastTimestamp
            );
        }

        // Get historical data within the duration window
        var historicalData = await _timeSeriesService.GetHistoricalDataAsync(
            dataSource,
            TimeSpan.FromMilliseconds(thresholdCondition.DurationMs)
        );

        if (!historicalData.Any())
        {
            _logger.Warning(
                "No historical data found for source {DataSource} within {Duration}ms",
                dataSource,
                thresholdCondition.DurationMs
            );
            return false;
        }

        // Add current value to historical data
        var allValues = historicalData.Append(currentValue);

        // Check if all values meet the threshold condition
        var allMeetThreshold = allValues.All(v => v >= thresholdCondition.Threshold);

        _metricsService.RecordThresholdEvaluation(
            dataSource,
            allMeetThreshold,
            thresholdCondition.DurationMs
        );

        return allMeetThreshold;
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
            _ => throw new ArgumentException(
                $"Invalid duration format: {duration}. Use s, m, h, or d suffix."
            ),
        };
    }
}
