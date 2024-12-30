using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Pulsar.Models;
using Pulsar.Models.Actions;
using Pulsar.RuleDefinition.Models;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Storage;  // Add this
using Serilog;
using Pulsar.Core.Services;  

namespace Pulsar.Runtime.Engine;

public class RuleEngine : IHostedService
{
    private readonly TimeSpan _cycleDuration;
    private readonly ILogger _logger;
    private readonly Core.Services.IMetricsService _metrics;  
    private readonly IDataStore _dataStore;
    private readonly IActionExecutor _actionExecutor;
    private readonly CompiledRuleSet _ruleSet;

    public RuleEngine(
        ILogger logger,
        Core.Services.IMetricsService metrics,  
        IDataStore dataStore,
        IActionExecutor actionExecutor,
        CompiledRuleSet ruleSet,
        TimeSpan? cycleDuration = null
    )
    {
        _logger = logger.ForContext<RuleEngine>();
        _metrics = metrics;
        _dataStore = dataStore;
        _actionExecutor = actionExecutor;
        _ruleSet = ruleSet;
        _cycleDuration = cycleDuration ?? TimeSpan.FromMilliseconds(100);

        _logger.Information(
            "Rule engine initialized with {RuleCount} rules in {LayerCount} layers",
            _ruleSet.Rules.Count,
            _ruleSet.LayerCount
        );
    }

    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information(
            "Starting rule engine execution cycle with {Duration}ms interval",
            _cycleDuration.TotalMilliseconds
        );
        return Task.CompletedTask;
    }

    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Stopping rule engine execution cycle");
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
            _logger.Error(ex, "Error during rule execution cycle");
        }
    }

    private async Task ExecuteRuleAsync(CompiledRule rule, IDictionary<string, double> data)
    {
        try
        {
            var conditionsMet = await EvaluateConditionsAsync(rule.Rule.Conditions, data);
            if (conditionsMet)
            {
                _logger.Debug("Rule {RuleName} conditions met, executing actions", rule.Rule.Name);
                foreach (var action in rule.Rule.Actions)
                {
                    await ExecuteActionAsync(action);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error executing rule {RuleName}", rule.Rule.Name);
        }
    }

    private async Task<bool> EvaluateConditionsAsync(
        ConditionGroup conditions,
        IDictionary<string, double> data
    )
    {
        if (conditions.All != null && conditions.All.Any())
        {
            foreach (var condition in conditions.All)
            {
                if (!await EvaluateConditionAsync(condition, data))
                {
                    return false;
                }
            }
            return true;
        }
        else if (conditions.Any != null && conditions.Any.Any())
        {
            foreach (var condition in conditions.Any)
            {
                if (await EvaluateConditionAsync(condition, data))
                {
                    return true;
                }
            }
            return false;
        }

        return true;
    }

    private async Task<bool> EvaluateConditionAsync(Condition condition, IDictionary<string, double> data)
    {
        try
        {
            switch (condition)
            {
                case ComparisonCondition comp:
                    if (!data.TryGetValue(comp.DataSource, out var value))
                    {
                        _logger.Warning("Data source {DataSource} not found", comp.DataSource);
                        return false;
                    }
                    return EvaluateComparison(value, comp.Operator, comp.Value);

                case ThresholdOverTimeCondition threshold:
                    var duration = TimeSpan.FromMilliseconds(threshold.DurationMs);
                    return await _dataStore.CheckThresholdOverTimeAsync(
                        threshold.DataSource,
                        threshold.Threshold,
                        duration
                    );

                default:
                    _logger.Warning("Unknown condition type: {Type}", condition.GetType().Name);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error evaluating condition");
            return false;
        }
    }

    private bool EvaluateComparison(double value, string op, double threshold)
    {
        return op switch
        {
            ">" => value > threshold,
            ">=" => value >= threshold,
            "<" => value < threshold,
            "<=" => value <= threshold,
            "==" => Math.Abs(value - threshold) < double.Epsilon,
            "!=" => Math.Abs(value - threshold) > double.Epsilon,
            _ => false
        };
    }

    private async Task ExecuteActionAsync(RuleAction action)
    {
        try
        {
            var compiledAction = new CompiledRuleAction();

            if (action.SetValue != null)
            {
                compiledAction.SetValue = new Models.Actions.SetValueAction
                {
                    Key = action.SetValue.Key,
                    Value = action.SetValue.Value,
                    ValueExpression = action.SetValue.ValueExpression
                };
            }
            else if (action.SendMessage != null)
            {
                compiledAction.SendMessage = new Models.Actions.SendMessageAction
                {
                    Channel = action.SendMessage.Channel,
                    Message = action.SendMessage.Message
                };
            }

            await _actionExecutor.ExecuteAsync(compiledAction);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error executing action");
        }
    }
}
