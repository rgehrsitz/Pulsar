using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Pulsar.Compiler.Models;
using Pulsar.RuleDefinition.Models;
using Serilog;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Executes compiled rules in a cyclic manner
/// </summary>
public class RuleEngine : BackgroundService
{
    private readonly TimeSpan _cycleDuration;
    private readonly ILogger _logger;
    private readonly ISensorDataProvider _sensorDataProvider;
    private readonly IActionExecutor _actionExecutor;
    private readonly CompiledRuleSet _ruleSet;
    private readonly Dictionary<string, RingBuffer<double>> _historicalValues;
    private readonly ComparisonEvaluator _comparisonEvaluator;
    private readonly ThresholdOverTimeEvaluator _thresholdEvaluator;

    public RuleEngine(
        ILogger logger,
        ISensorDataProvider sensorDataProvider,
        IActionExecutor actionExecutor,
        CompiledRuleSet ruleSet,
        TimeSpan? cycleDuration = null)
    {
        _logger = logger.ForContext<RuleEngine>();
        _sensorDataProvider = sensorDataProvider;
        _actionExecutor = actionExecutor;
        _ruleSet = ruleSet;
        _cycleDuration = cycleDuration ?? TimeSpan.FromMilliseconds(100);

        // Initialize evaluators
        _comparisonEvaluator = new ComparisonEvaluator(_logger);
        _thresholdEvaluator = new ThresholdOverTimeEvaluator(_sensorDataProvider, _logger);

        // Initialize historical value buffers for all input sensors
        _historicalValues = new Dictionary<string, RingBuffer<double>>();
        foreach (var sensor in _ruleSet.AllInputSensors)
        {
            _historicalValues[sensor] = new RingBuffer<double>(100); // Store last 100 values
        }

        _logger.Information("Initialized RuleEngine with {RuleCount} rules in {LayerCount} layers", 
            _ruleSet.Rules.Count, _ruleSet.LayerCount);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Starting rule engine execution cycle with {Duration}ms interval", _cycleDuration.TotalMilliseconds);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteCycle();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing rule cycle");
            }

            await Task.Delay(_cycleDuration, stoppingToken);
        }

        _logger.Information("Rule engine execution stopped");
    }

    private async Task ExecuteCycle()
    {
        _logger.Debug("Starting new rule cycle");

        // Get all sensor values in bulk
        var sensorData = await _sensorDataProvider.GetCurrentDataAsync();
        _logger.Debug("Retrieved {Count} sensor values", sensorData.Count);

        // Update historical values
        foreach (var (key, value) in sensorData)
        {
            if (_historicalValues.TryGetValue(key, out var buffer))
            {
                buffer.Add(value);
                _logger.Verbose("Updated historical values for sensor {Sensor}: {Value}", key, value);
            }
        }

        // Execute rules layer by layer (rules in each layer can be parallelized)
        var currentLayer = -1;
        var layerRules = new List<CompiledRule>();

        foreach (var rule in _ruleSet.Rules)
        {
            if (rule.Layer != currentLayer)
            {
                // Process previous layer
                if (layerRules.Any())
                {
                    _logger.Debug("Processing layer {Layer} with {Count} rules", currentLayer, layerRules.Count);
                    await ProcessRuleLayer(layerRules, sensorData);
                    layerRules.Clear();
                }
                currentLayer = rule.Layer;
            }
            layerRules.Add(rule);
        }

        // Process final layer
        if (layerRules.Any())
        {
            _logger.Debug("Processing final layer {Layer} with {Count} rules", currentLayer, layerRules.Count);
            await ProcessRuleLayer(layerRules, sensorData);
        }

        _logger.Debug("Rule cycle completed");
    }

    private async Task ProcessRuleLayer(
        IReadOnlyList<CompiledRule> layerRules,
        IDictionary<string, double> sensorData)
    {
        _logger.Debug("Processing {Count} rules in parallel", layerRules.Count);

        // Execute rules in parallel
        var tasks = layerRules.Select(async rule =>
        {
            try
            {
                _logger.Debug("Evaluating rule {RuleName}", rule.Rule.Name);
                var conditionMet = await EvaluateConditions(rule.Rule.Conditions, sensorData);
                
                if (conditionMet)
                {
                    _logger.Information("Rule {RuleName} conditions met, executing {ActionCount} actions", 
                        rule.Rule.Name, rule.Rule.Actions.Count);

                    foreach (var action in rule.Rule.Actions)
                    {
                        if (action.SetValue != null)
                        {
                            _logger.Debug("Executing SetValue action for rule {RuleName}: {@Values}", 
                                rule.Rule.Name, action.SetValue);

                            // Update sensor data with new values
                            foreach (var (key, value) in action.SetValue)
                            {
                                if (double.TryParse(value?.ToString(), out var doubleValue))
                                {
                                    sensorData[key] = doubleValue;
                                    _logger.Debug("Updated sensor {Sensor} to value {Value}", key, doubleValue);
                                }
                                else
                                {
                                    _logger.Warning("Could not parse value {Value} for sensor {Sensor}", value, key);
                                }
                            }
                            await _sensorDataProvider.SetSensorDataAsync(action.SetValue);
                        }
                        else
                        {
                            _logger.Debug("Executing custom action for rule {RuleName}: {ActionType}", 
                                rule.Rule.Name, action.GetType().Name);
                            await _actionExecutor.ExecuteAsync(action);
                        }
                    }
                }
                else
                {
                    _logger.Debug("Rule {RuleName} conditions not met", rule.Rule.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error evaluating rule {RuleName}", rule.Rule.Name);
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task<bool> EvaluateConditions(ConditionGroup group, IDictionary<string, double> sensorData)
    {
        if (group.All != null && group.All.Any())
        {
            _logger.Debug("Evaluating ALL conditions group with {Count} conditions", group.All.Count);
            foreach (var condition in group.All)
            {
                if (!await EvaluateCondition(condition, sensorData))
                {
                    _logger.Debug("ALL conditions group failed - condition not met");
                    return false;
                }
            }
            _logger.Debug("ALL conditions group passed - all conditions met");
            return true;
        }

        if (group.Any != null && group.Any.Any())
        {
            _logger.Debug("Evaluating ANY conditions group with {Count} conditions", group.Any.Count);
            foreach (var condition in group.Any)
            {
                if (await EvaluateCondition(condition, sensorData))
                {
                    _logger.Debug("ANY conditions group passed - condition met");
                    return true;
                }
            }
            _logger.Debug("ANY conditions group failed - no conditions met");
            return false;
        }

        _logger.Debug("Empty condition group - returning true");
        return true; // Empty condition group is always true
    }

    private async Task<bool> EvaluateCondition(Condition condition, IDictionary<string, double> sensorData)
    {
        try
        {
            _logger.Debug("Evaluating condition of type {ConditionType}", condition.GetType().Name);
            var result = condition switch
            {
                ComparisonCondition => await _comparisonEvaluator.EvaluateAsync(condition, sensorData),
                ThresholdOverTimeCondition => await _thresholdEvaluator.EvaluateAsync(condition, sensorData),
                _ => throw new ArgumentException($"Unsupported condition type: {condition.GetType().Name}")
            };

            _logger.Debug("Condition evaluation result: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error evaluating condition of type {ConditionType}", condition.GetType().Name);
            return false;
        }
    }
}
