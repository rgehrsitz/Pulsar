using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.RuleDefinition.Analysis;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Conditions;
using Serilog;

namespace Pulsar.RuleDefinition.Validation;

public class RuleValidator
{
    private readonly SystemConfig _config;
    private readonly DependencyAnalyzer _dependencyAnalyzer;
    private readonly ILogger _logger;
    private static readonly HashSet<string> ValidOperators = new()
    {
        ">",
        "<",
        ">=",
        "<=",
        "==",
        "!=",
    };

    public RuleValidator(SystemConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _dependencyAnalyzer = new DependencyAnalyzer();
        _logger = Log.ForContext<RuleValidator>();
    }

    public ValidationResult ValidateRuleSet(RuleSetDefinition ruleSet)
    {
        var result = new ValidationResult();

        if (ruleSet == null)
        {
            result.AddError("RuleSet cannot be null");
            return result;
        }

        if (ruleSet.Rules == null || !ruleSet.Rules.Any())
        {
            result.AddError("RuleSet must contain at least one rule");
            return result;
        }

        // Validate each rule
        foreach (var rule in ruleSet.Rules)
        {
            var ruleValidation = ValidateRule(rule);
            result.Merge(ruleValidation);
        }

        // Check for circular dependencies
        var analyzer = new DependencyAnalyzer();
        var (orderedRules, cyclicDependencies) = analyzer.AnalyzeAndOrder(ruleSet);
        
        // If there are circular dependencies, add them as errors
        if (cyclicDependencies.Any())
        {
            result.AddError($"Circular dependencies detected: {string.Join(" -> ", cyclicDependencies)}");
        }

        return result;
    }

    private ValidationResult ValidateRule(RuleDefinitionModel rule)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            result.AddError("Rule name is required");
        }

        if (rule.Conditions == null)
        {
            result.AddError("Rule must have conditions");
            return result;
        }

        var conditionValidation = ValidateConditionGroup(rule.Conditions);
        foreach (var error in conditionValidation)
        {
            result.AddError(error);
        }

        if (rule.Actions == null || !rule.Actions.Any())
        {
            result.AddError("Rule must have at least one action");
            return result;
        }

        foreach (var action in rule.Actions)
        {
            var actionValidation = ValidateAction(action);
            result.Merge(actionValidation);
        }

        return result;
    }

    private ValidationResult ValidateAction(RuleAction action)
    {
        var result = new ValidationResult();

        if (action.SetValue == null)
        {
            result.AddError("Action must have a SetValue property");
            return result;
        }

        if (string.IsNullOrWhiteSpace(action.SetValue.Key))
        {
            result.AddError("SetValue action must have a key");
        }

        if (action.SetValue.Value == null)
        {
            result.AddError("SetValue action must have a value");
        }

        return result;
    }

    private List<string> ValidateConditionGroup(ConditionGroupDefinition group)
    {
        var errors = new List<string>();

        if ((group.All == null || !group.All.Any()) && (group.Any == null || !group.Any.Any()))
        {
            errors.Add("Must have at least one condition");
            return errors;
        }

        if (group.All != null)
        {
            foreach (var condition in group.All)
            {
                var conditionErrors = ValidateCondition(condition);
                errors.AddRange(conditionErrors);
            }
        }

        if (group.Any != null)
        {
            foreach (var condition in group.Any)
            {
                var conditionErrors = ValidateCondition(condition);
                errors.AddRange(conditionErrors);
            }
        }

        return errors;
    }

    private List<string> ValidateCondition(ConditionDefinition wrapper)
    {
        var errors = new List<string>();

        if (wrapper.Condition == null)
        {
            errors.Add("Condition is required");
            return errors;
        }

        switch (wrapper.Condition)
        {
            case ComparisonConditionDefinition comparison:
                errors.AddRange(ValidateComparisonCondition(comparison));
                break;
            case ThresholdOverTimeConditionDefinition temporal:
                errors.AddRange(ValidateTemporalCondition(temporal));
                break;
            default:
                errors.Add($"Unknown condition type: {wrapper.Condition.GetType().Name}");
                break;
        }

        return errors;
    }

    private List<string> ValidateComparisonCondition(ComparisonConditionDefinition condition)
    {
        var errors = new List<string>();

        // Validate data source
        if (string.IsNullOrWhiteSpace(condition.DataSource))
        {
            errors.Add("Data source is required");
        }
        else if (!_config.ValidSensors.Contains(condition.DataSource))
        {
            errors.Add($"Invalid data source: {condition.DataSource}");
        }

        // Validate operator
        if (string.IsNullOrWhiteSpace(condition.Operator))
        {
            errors.Add("Operator is required");
        }
        else if (!ValidOperators.Contains(condition.Operator))
        {
            errors.Add($"Invalid operator: {condition.Operator}");
        }

        // Validate value
        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            errors.Add("Value is required");
        }
        else if (!double.TryParse(condition.Value, out var value) || double.IsNaN(value))
        {
            errors.Add("Invalid numeric value");
        }

        return errors;
    }

    private List<string> ValidateTemporalCondition(ThresholdOverTimeConditionDefinition condition)
    {
        var errors = new List<string>();

        // Validate data source
        if (string.IsNullOrWhiteSpace(condition.DataSource))
        {
            errors.Add("Data source is required");
        }
        else if (!_config.ValidSensors.Contains(condition.DataSource))
        {
            errors.Add($"Invalid data source: {condition.DataSource}");
        }

        // Validate threshold
        if (string.IsNullOrWhiteSpace(condition.Threshold))
        {
            errors.Add("Threshold is required");
        }
        else if (!double.TryParse(condition.Threshold, out var threshold) || double.IsNaN(threshold))
        {
            errors.Add("Invalid threshold value");
        }

        // Validate duration
        if (string.IsNullOrWhiteSpace(condition.Duration))
        {
            errors.Add("Duration is required");
        }
        else
        {
            var temporalValidator = new TemporalValidator();
            var (isValid, _, error) = temporalValidator.ValidateDuration(condition.Duration);
            if (!isValid)
            {
                errors.Add(error);
            }
        }

        return errors;
    }
}
