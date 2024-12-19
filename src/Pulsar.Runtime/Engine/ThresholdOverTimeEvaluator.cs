using System;
using System.Threading.Tasks;
using System.Linq;
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

    public ThresholdOverTimeEvaluator(
        ISensorDataProvider dataProvider,
        ILogger logger,
        TimeSpan? samplingRate = null)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _logger = logger.ForContext<ThresholdOverTimeEvaluator>();
        _validator = new TemporalValidator();
        _timeSeriesService = new TimeSeriesService(logger);
        _samplingRate = samplingRate ?? TimeSpan.FromSeconds(1);
    }

    public async Task<bool> EvaluateAsync(Condition condition, IDictionary<string, double> sensorData)
    {
        if (condition is not ThresholdOverTimeCondition thresholdCondition)
        {
            throw new ArgumentException($"Expected ThresholdOverTimeCondition but got {condition.GetType().Name}");
        }

        // Validate the condition
        var errors = _validator.ValidateTemporalCondition(thresholdCondition);
        if (errors.Any())
        {
            _logger.Error("Invalid temporal condition: {Errors}", string.Join(", ", errors));
            return false;
        }

        var dataSource = thresholdCondition.DataSource;
        if (!sensorData.ContainsKey(dataSource))
        {
            _logger.Warning("Data source not found: {DataSource}", dataSource);
            return false;
        }

        // Update the time series with the current value
        var currentValue = sensorData[dataSource];
        var now = DateTime.UtcNow;
        var lastTimestamp = _timeSeriesService.GetTimeWindow(dataSource, TimeSpan.FromMilliseconds(1)).LastOrDefault().Timestamp;

        // Only add the value if enough time has passed since the last update
        if (lastTimestamp == default || now - lastTimestamp >= _samplingRate)
        {
            _timeSeriesService.Update(dataSource, currentValue);
        }

        // Get historical data from the time series
        var duration = TimeSpan.FromMilliseconds(thresholdCondition.DurationMs);
        var values = _timeSeriesService.GetTimeWindow(dataSource, duration);

        if (values.Length == 0)
        {
            _logger.Debug("No historical data available for evaluation");
            return false;
        }

        // Calculate the percentage of values that meet the threshold condition
        var threshold = thresholdCondition.Threshold;
        var requiredPercentage = thresholdCondition.RequiredPercentage;
        var valuesAboveThreshold = values.Count(x => x.Value >= threshold);
        var valuesBelowThreshold = values.Count(x => x.Value <= threshold);
        var totalValues = values.Length;

        // Calculate the actual percentage based on the operator
        var actualPercentage = thresholdCondition.Operator switch
        {
            ThresholdOperator.GreaterThan => (double)valuesAboveThreshold / totalValues,
            ThresholdOperator.LessThan => (double)valuesBelowThreshold / totalValues,
            _ => throw new ArgumentException($"Unsupported threshold operator: {thresholdCondition.Operator}")
        };

        // Log the percentages for debugging
        _logger.Debug(
            "Threshold evaluation: {Operator} {Threshold}, {ActualPercentage:P2} of values meet condition (required: {RequiredPercentage:P2})",
            thresholdCondition.Operator,
            threshold,
            actualPercentage,
            requiredPercentage);

        return actualPercentage >= requiredPercentage;
    }
}
