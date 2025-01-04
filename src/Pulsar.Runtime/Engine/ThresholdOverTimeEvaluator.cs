using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Core;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Conditions;
using Pulsar.RuleDefinition.Validation;
using Pulsar.Runtime.Services;
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
        ConditionDefinition condition,
        IDictionary<string, double> sensorData
    )
    {
        if (condition?.Condition is not ThresholdOverTimeConditionDefinition temporal)
        {
            _logger.Error(
                "Invalid condition type. Expected {ExpectedType} but got {ActualType}",
                typeof(ThresholdOverTimeConditionDefinition).Name,
                condition?.GetType().Name ?? "null"
            );
            return false;
        }

        if (string.IsNullOrWhiteSpace(temporal.DataSource))
        {
            _logger.Error("Data source is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(temporal.Threshold))
        {
            _logger.Error("Threshold is required");
            return false;
        }

        if (!double.TryParse(temporal.Threshold, out var threshold))
        {
            _logger.Error("Invalid threshold value: {Value}", temporal.Threshold);
            return false;
        }

        if (string.IsNullOrWhiteSpace(temporal.Duration))
        {
            _logger.Error("Duration is required");
            return false;
        }

        var (isValid, durationMs, error) = _validator.ValidateDuration(temporal.Duration);
        if (!isValid)
        {
            _logger.Error("Invalid duration: {Error}", error);
            return false;
        }

        var duration = TimeSpan.FromMilliseconds(durationMs);
        var (isValidPoints, requiredPoints, pointsError) = _validator.CalculateRequiredDataPoints(
            temporal.Duration,
            _samplingRate
        );

        if (!isValidPoints)
        {
            _logger.Error("Invalid data points: {Error}", pointsError);
            return false;
        }

        var endTime = DateTime.UtcNow;
        var startTime = endTime - duration;

        _logger.Debug(
            "Fetching historical data for {DataSource} from {StartTime} to {EndTime}",
            temporal.DataSource,
            startTime,
            endTime
        );

        var historicalData = await _timeSeriesService.GetDataPointsAsync(
            temporal.DataSource,
            startTime,
            endTime,
            _samplingRate
        );

        if (!historicalData.Any())
        {
            _logger.Warning(
                "No historical data found for {DataSource} between {StartTime} and {EndTime}",
                temporal.DataSource,
                startTime,
                endTime
            );
            return false;
        }

        var result = historicalData.All(point => point.Value > threshold);

        _logger.Debug(
            "Evaluated threshold over time condition: {DataSource} > {Threshold} for {Duration} = {Result}",
            temporal.DataSource,
            temporal.Threshold,
            temporal.Duration,
            result
        );

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
            _ => throw new ArgumentException(
                $"Invalid duration format: {duration}. Use s, m, h, or d suffix."
            ),
        };
    }
}
