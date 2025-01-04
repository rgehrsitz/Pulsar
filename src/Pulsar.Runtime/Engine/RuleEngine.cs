using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Models.Actions;
using Pulsar.RuleDefinition.Models;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Storage;
using Serilog;
using Pulsar.Core.Services;

namespace Pulsar.Runtime.Engine;

public class RuleEngine : IHostedService
{
    private readonly TimeSpan _cycleDuration;
    private readonly ILogger<RuleEngine> _logger;
    private readonly Core.Services.IMetricsService _metrics;  
    private readonly IDataStore _dataStore;
    private readonly IActionExecutor _actionExecutor;
    private readonly CompiledRuleSet _ruleSet;
    private readonly IConditionEvaluator _comparisonEvaluator;
    private readonly IConditionEvaluator _thresholdEvaluator;

    public RuleEngine(
        ILogger<RuleEngine> logger,
        Core.Services.IMetricsService metrics,  
        IDataStore dataStore,
        IActionExecutor actionExecutor,
        CompiledRuleSet ruleSet,
        TimeSpan? cycleDuration = null,
        IConditionEvaluator comparisonEvaluator = null,
        IConditionEvaluator thresholdEvaluator = null
    )
    {
        _logger = logger;
        _metrics = metrics;
        _dataStore = dataStore;
        _actionExecutor = actionExecutor;
        _ruleSet = ruleSet;
        _cycleDuration = cycleDuration ?? TimeSpan.FromMilliseconds(100);
        _comparisonEvaluator = comparisonEvaluator;
        _thresholdEvaluator = thresholdEvaluator;

        _logger.LogInformation(
            "Rule engine initialized with {RuleCount} rules in {LayerCount} layers",
            _ruleSet.Rules.Count,
            _ruleSet.LayerCount
        );
    }

    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting rule engine execution cycle with {Duration}ms interval",
            _cycleDuration.TotalMilliseconds
        );
        return Task.CompletedTask;
    }

    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping rule engine execution cycle");
        return Task.CompletedTask;
    }

    public async Task ExecuteCycleAsync()
    {
        try
        {
            var data = await _dataStore.GetCurrentDataAsync();
            foreach (var rule in _ruleSet.Rules)
            {
                await ExecuteRuleAsync(rule, data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rule execution cycle");
        }
    }

    private async Task ExecuteRuleAsync(CompiledRule rule, IDictionary<string, double> data)
    {
        try
        {
            var conditionsMet = await EvaluateConditionGroupAsync(rule.RuleDefinition.Conditions, data);
            if (conditionsMet)
            {
                _logger.LogDebug("Rule {RuleName} conditions met, executing actions", rule.RuleDefinition.Name);
                await ExecuteActionsAsync(rule.RuleDefinition, data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing rule {RuleName}", rule.RuleDefinition.Name);
        }
    }

    private async Task<bool> EvaluateConditionGroupAsync(
        ConditionGroupDefinition group,
        IDictionary<string, double> sensorData
    )
    {
        if (group.All != null && group.All.Any())
        {
            foreach (var condition in group.All)
            {
                if (!await EvaluateConditionAsync(condition, sensorData))
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
                if (await EvaluateConditionAsync(condition, sensorData))
                {
                    return true;
                }
            }
            return false;
        }

        _logger.LogWarning("Condition group has no conditions");
        return false;
    }

    private async Task<bool> EvaluateConditionAsync(
        ConditionDefinition condition,
        IDictionary<string, double> sensorData
    )
    {
        if (condition.Condition == null)
        {
            _logger.LogWarning("Condition is null");
            return false;
        }

        var evaluator = GetEvaluator(condition.Condition);
        if (evaluator == null)
        {
            _logger.LogWarning(
                "No evaluator found for condition type {ConditionType}",
                condition.Condition.GetType().Name
            );
            return false;
        }

        return await evaluator.EvaluateAsync(condition, sensorData);
    }

    private IConditionEvaluator GetEvaluator(IConditionDefinition condition)
    {
        return condition switch
        {
            ComparisonConditionDefinition => _comparisonEvaluator,
            ThresholdOverTimeConditionDefinition => _thresholdEvaluator,
            _ => null
        };
    }

    private async Task ExecuteActionsAsync(
        RuleDefinitionModel rule,
        IDictionary<string, double> sensorData
    )
    {
        foreach (var action in rule.Actions)
        {
            try
            {
                await _actionExecutor.ExecuteAsync(action, sensorData);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to execute action {ActionType} for rule {RuleName}",
                    action.GetType().Name,
                    rule.Name
                );
            }
        }
    }
}
