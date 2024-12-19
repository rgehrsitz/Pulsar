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
        _comparisonEvaluator = new ComparisonEvaluator();
        _thresholdEvaluator = new ThresholdOverTimeEvaluator(_sensorDataProvider);

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
    }

    private async Task ExecuteCycle()
    {
        // Get all sensor values in bulk
        var sensorData = await _sensorDataProvider.GetCurrentDataAsync();

        // Update historical values
        foreach (var (key, value) in sensorData)
        {
            if (_historicalValues.TryGetValue(key, out var buffer))
            {
                buffer.Add(value);
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
            await ProcessRuleLayer(layerRules, sensorData);
        }
    }

    private async Task ProcessRuleLayer(
        IReadOnlyList<CompiledRule> layerRules,
        IDictionary<string, double> sensorData)
    {
        // Execute rules in parallel
        var tasks = layerRules.Select(async rule =>
        {
            try
            {
                var conditionMet = await EvaluateConditions(rule.Rule.Conditions, sensorData);
                if (conditionMet)
                {
                    foreach (var action in rule.Rule.Actions)
                    {
                        if (action.SetValue != null)
                        {
                            // Update sensor data with new values
                            foreach (var (key, value) in action.SetValue)
                            {
                                if (double.TryParse(value?.ToString(), out var doubleValue))
                                {
                                    sensorData[key] = doubleValue;
                                }
                            }
                            await _sensorDataProvider.SetSensorDataAsync(action.SetValue);
                        }
                        else
                        {
                            await _actionExecutor.ExecuteAsync(action);
                        }
                    }
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
            foreach (var condition in group.All)
            {
                if (!await EvaluateCondition(condition, sensorData))
                {
                    return false;
                }
            }
            return true;
        }

        if (group.Any != null && group.Any.Any())
        {
            foreach (var condition in group.Any)
            {
                if (await EvaluateCondition(condition, sensorData))
                {
                    return true;
                }
            }
            return false;
        }

        return true; // Empty condition group is always true
    }

    private async Task<bool> EvaluateCondition(Condition condition, IDictionary<string, double> sensorData)
    {
        try
        {
            return condition switch
            {
                ComparisonCondition => await _comparisonEvaluator.EvaluateAsync(condition, sensorData),
                ThresholdOverTimeCondition => await _thresholdEvaluator.EvaluateAsync(condition, sensorData),
                _ => throw new ArgumentException($"Unsupported condition type: {condition.GetType().Name}")
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error evaluating condition");
            return false;
        }
    }
}
