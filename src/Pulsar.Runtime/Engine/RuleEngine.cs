using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Pulsar.Compiler.Models;
using Pulsar.RuleDefinition.Models;
using Pulsar.Runtime.Services;
using Serilog;

namespace Pulsar.Runtime.Engine;

public class RuleEngine : IHostedService
{
    private readonly TimeSpan _cycleDuration;
    private readonly ILogger _logger;
    private readonly MetricsService _metrics;
    private readonly ISensorDataProvider _sensorDataProvider;
    private readonly IActionExecutor _actionExecutor;
    private readonly CompiledRuleSet _ruleSet;

    public RuleEngine(
        ILogger logger,
        MetricsService metrics,
        ISensorDataProvider sensorDataProvider,
        IActionExecutor actionExecutor,
        CompiledRuleSet ruleSet,
        TimeSpan? cycleDuration = null)
    {
        _logger = logger.ForContext<RuleEngine>();
        _metrics = metrics;
        _sensorDataProvider = sensorDataProvider;
        _actionExecutor = actionExecutor;
        _ruleSet = ruleSet;
        _cycleDuration = cycleDuration ?? TimeSpan.FromSeconds(1);

        _logger.Information("Rule engine initialized with {RuleCount} rules in {LayerCount} layers", 
            _ruleSet.Rules.Count, _ruleSet.LayerCount);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Starting rule engine execution cycle with {Duration}ms interval", _cycleDuration.TotalMilliseconds);
        
        return ExecuteAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Rule engine execution stopped");
        return Task.CompletedTask;
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteCycle();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during rule execution cycle");
            }

            await Task.Delay(_cycleDuration, stoppingToken);
        }
    }

    private async Task ExecuteCycle()
    {
        _logger.Debug("Starting rule execution cycle");
        var sensorData = await _sensorDataProvider.GetCurrentDataAsync();

        var tasks = _ruleSet.Rules.Select(async rule =>
        {
            try
            {
                using var _ = _metrics.MeasureRuleExecutionDuration(rule.Rule.Name);
                _metrics.RecordRuleExecution(rule.Rule.Name);

                _logger.Debug("Evaluating rule {RuleName}", rule.Rule.Name);
                var conditionMet = await EvaluateConditions(rule.Rule, rule.Rule.Conditions, sensorData);

                if (conditionMet)
                {
                    _logger.Information("Rule {RuleName} conditions met, executing {ActionCount} actions", 
                        rule.Rule.Name, rule.Rule.Actions.Count);
                    await ExecuteActions(rule.Rule, rule.Rule.Actions, CancellationToken.None);
                }
                else
                {
                    _logger.Debug("Rule {RuleName} conditions not met, skipping actions", rule.Rule.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error evaluating rule {RuleName}", rule.Rule.Name);
                _metrics.RecordRuleExecutionError(rule.Rule.Name, ex.GetType().Name);
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task ExecuteActions(Rule rule, IEnumerable<RuleAction> actions, CancellationToken cancellationToken)
    {
        foreach (var action in actions)
        {
            try
            {
                _logger.Debug("Executing action {ActionType}", action.GetType().Name);
                _metrics.RecordActionExecution(rule.Name, action.GetType().Name);

                if (action.SetValue != null)
                {
                    _logger.Debug("Executing SetValue action: {@Values}", action.SetValue);
                    await _sensorDataProvider.SetSensorDataAsync(action.SetValue);
                }
                else
                {
                    _logger.Debug("Executing custom action: {ActionType}", action.GetType().Name);
                    await _actionExecutor.ExecuteAsync(action);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing action {ActionType}", action.GetType().Name);
                _metrics.RecordActionExecutionError(rule.Name, action.GetType().Name, ex.GetType().Name);
            }
        }
    }

    private async Task<bool> EvaluateConditions(Rule rule, ConditionGroup group, IDictionary<string, double> sensorData)
    {
        if (group.All != null && group.All.Any())
        {
            _logger.Debug("Evaluating ALL conditions group with {Count} conditions", group.All.Count);
            foreach (var condition in group.All)
            {
                var result = await EvaluateCondition(condition, sensorData);
                _metrics.RecordConditionEvaluation(rule.Name, condition.GetType().Name, result);

                if (!result)
                {
                    _logger.Debug("ALL conditions group failed - condition not met");
                    return false;
                }
            }
            return true;
        }

        if (group.Any != null && group.Any.Any())
        {
            _logger.Debug("Evaluating ANY conditions group with {Count} conditions", group.Any.Count);
            foreach (var condition in group.Any)
            {
                var result = await EvaluateCondition(condition, sensorData);
                _metrics.RecordConditionEvaluation(rule.Name, condition.GetType().Name, result);

                if (result)
                {
                    _logger.Debug("ANY conditions group passed - condition met");
                    return true;
                }
            }
        }

        _logger.Debug("No conditions met in ANY group");
        return false;
    }

    private Task<bool> EvaluateCondition(Condition condition, IDictionary<string, double> sensorData)
    {
        if (condition is ComparisonCondition comparison)
        {
            if (!sensorData.TryGetValue(comparison.DataSource, out var value))
            {
                _logger.Warning("Data source {DataSource} not found in sensor data", comparison.DataSource);
                return Task.FromResult(false);
            }

            var result = comparison.Operator switch
            {
                "==" => Math.Abs(value - comparison.Value) < 0.0001,
                "!=" => Math.Abs(value - comparison.Value) >= 0.0001,
                ">" => value > comparison.Value,
                ">=" => value >= comparison.Value,
                "<" => value < comparison.Value,
                "<=" => value <= comparison.Value,
                _ => false
            };

            _logger.Debug("Evaluated comparison condition: {DataSource} {Operator} {Value} = {Result}",
                comparison.DataSource, comparison.Operator, comparison.Value, result);

            return Task.FromResult(result);
        }

        _logger.Warning("Unsupported condition type: {ConditionType}", condition.GetType().Name);
        return Task.FromResult(false);
    }
}
