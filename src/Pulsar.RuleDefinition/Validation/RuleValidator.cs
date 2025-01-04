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
        _logger.Information("Starting validation of ruleset version {Version}", ruleSet?.Version);
        var errors = new List<ValidationError>();

        if (ruleSet == null)
        {
            _logger.Error("RuleSet is null");
            throw new ArgumentNullException(nameof(ruleSet));
        }

        if (ruleSet.Version <= 0)
        {
            _logger.Warning("Invalid version {Version} in ruleset", ruleSet.Version);
            errors.Add(new ValidationError("Version must be greater than 0"));
        }

        if (ruleSet.Rules == null || !ruleSet.Rules.Any())
        {
            _logger.Warning("Empty rules list in ruleset");
            errors.Add(new ValidationError("Rules list cannot be empty"));
            return new ValidationResult(errors.Select(e => e.Message).ToList());
        }

        _logger.Debug(
            "Checking for duplicate rule names in {RuleCount} rules",
            ruleSet.Rules.Count
        );
        // Check for duplicate rule names
        var duplicateRules = ruleSet
            .Rules.GroupBy(r => r.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateRules.Any())
        {
            foreach (var ruleName in duplicateRules)
            {
                _logger.Warning("Duplicate rule name found: {RuleName}", ruleName);
                errors.Add(new ValidationError($"Duplicate rule name: {ruleName}"));
            }
        }

        _logger.Debug("Validating individual rules");
        // Validate each rule
        foreach (var rule in ruleSet.Rules)
        {
            _logger.Debug("Validating rule {RuleName}", rule.Name);
            var ruleErrors = ValidateRule(rule);
            if (ruleErrors.Any())
            {
                _logger.Warning(
                    "Found {ErrorCount} validation errors in rule {RuleName}: {@Errors}",
                    ruleErrors.Count,
                    rule.Name,
                    ruleErrors
                );
                errors.AddRange(ruleErrors);
            }
        }

        // Check for cyclic dependencies
        _logger.Debug("Checking for cyclic dependencies");
        var (_, cyclicDependencies) = _dependencyAnalyzer.AnalyzeAndOrder(ruleSet);
        if (cyclicDependencies.Any())
        {
            foreach (var cycle in cyclicDependencies)
            {
                _logger.Error("Found cyclic dependency: {CyclePath}", cycle);
                errors.Add(new ValidationError($"Cyclic dependency detected: {cycle}"));
            }
        }

        if (errors.Any())
        {
            _logger.Warning("Validation completed with {ErrorCount} errors", errors.Count);
        }
        else
        {
            _logger.Information("Validation completed successfully");
        }

        return new ValidationResult(errors.Select(e => e.Message).ToList());
    }

    private List<ValidationError> ValidateRule(RuleDefinitionModel rule)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            _logger.Warning("Rule has no name");
            errors.Add(new ValidationError("Rule name is required"));
        }

        if (rule.Conditions == null)
        {
            _logger.Warning("Rule '{RuleName}' has no conditions", rule.Name);
            errors.Add(new ValidationError($"Rule '{rule.Name}' has no conditions"));
        }
        else
        {
            // Validate All conditions
            if (rule.Conditions.All != null)
            {
                foreach (var condition in rule.Conditions.All)
                {
                    var conditionErrors = ValidateCondition(condition.Condition, rule.Name);
                    if (conditionErrors.Any())
                    {
                        _logger.Warning(
                            "Found {ErrorCount} errors in condition of rule {RuleName}: {@Errors}",
                            conditionErrors.Count,
                            rule.Name,
                            conditionErrors
                        );
                        errors.AddRange(conditionErrors);
                    }
                }
            }

            // Validate Any conditions
            if (rule.Conditions.Any != null)
            {
                foreach (var condition in rule.Conditions.Any)
                {
                    var conditionErrors = ValidateCondition(condition.Condition, rule.Name);
                    if (conditionErrors.Any())
                    {
                        _logger.Warning(
                            "Found {ErrorCount} errors in condition of rule {RuleName}: {@Errors}",
                            conditionErrors.Count,
                            rule.Name,
                            conditionErrors
                        );
                        errors.AddRange(conditionErrors);
                    }
                }
            }

            // At least one of All or Any must be present and non-empty
            if (
                (rule.Conditions.All == null || !rule.Conditions.All.Any())
                && (rule.Conditions.Any == null || !rule.Conditions.Any.Any())
            )
            {
                _logger.Warning("Rule '{RuleName}' has no conditions", rule.Name);
                errors.Add(new ValidationError($"Rule '{rule.Name}' has no conditions"));
            }
        }

        // Validate actions
        if (rule.Actions == null || !rule.Actions.Any())
        {
            _logger.Warning("Rule '{RuleName}' has no actions", rule.Name);
            errors.Add(new ValidationError($"Rule '{rule.Name}' has no actions"));
        }
        else
        {
            foreach (var action in rule.Actions)
            {
                var actionErrors = ValidateRuleAction(action, rule.Name);
                if (actionErrors.Any())
                {
                    _logger.Warning(
                        "Found {ErrorCount} errors in action of rule {RuleName}: {@Errors}",
                        actionErrors.Count,
                        rule.Name,
                        actionErrors
                    );
                    errors.AddRange(actionErrors);
                }
            }
        }

        return errors;
    }

    private List<ValidationError> ValidateCondition(Condition condition, string ruleName)
    {
        var errors = new List<ValidationError>();
        switch (condition)
        {
            case ComparisonConditionDefinition comparison:
                if (comparison.DataSource != null && !_config.ValidSensors.Contains(comparison.DataSource))
                {
                    _logger.Warning("Invalid data source: {DataSource}", comparison.DataSource);
                    errors.Add(new ValidationError($"Invalid data source: {comparison.DataSource}"));
                }

                if (!ValidOperators.Contains(comparison.Operator))
                {
                    _logger.Warning("Invalid operator: {Operator}", comparison.Operator);
                    errors.Add(new ValidationError($"Invalid operator: {comparison.Operator}"));
                }
                break;

            case ThresholdOverTimeConditionDefinition threshold:
                if (threshold.DataSource != null && !_config.ValidSensors.Contains(threshold.DataSource))
                {
                    _logger.Warning("Invalid data source: {DataSource}", threshold.DataSource);
                    errors.Add(new ValidationError($"Invalid data source: {threshold.DataSource}"));
                }
                break;

            case ExpressionConditionDefinition expression:
                // Add any validation for expression conditions here
                break;
        }

        return errors;
    }

    private List<ValidationError> ValidateRuleAction(RuleAction action, string ruleName)
    {
        var errors = new List<ValidationError>();

        // Each action must have exactly one of SetValue or SendMessage
        if ((action.SetValue == null && action.SendMessage == null) ||
            (action.SetValue != null && action.SendMessage != null))
        {
            _logger.Warning("Rule '{RuleName}' has an invalid action: must have exactly one of set_value or send_message", ruleName);
            errors.Add(
                new ValidationError($"Rule '{ruleName}' has an invalid action: must have exactly one of set_value or send_message")
            );
            return errors;
        }

        // Validate SetValue action if present
        if (action.SetValue != null)
        {
            if (string.IsNullOrEmpty(action.SetValue.Key))
            {
                _logger.Warning("Rule '{RuleName}' has an invalid action: missing key", ruleName);
                errors.Add(
                    new ValidationError($"Rule '{ruleName}' has an invalid action: missing key")
                );
            }
            else if (!_config.ValidSensors.Contains(action.SetValue.Key))
            {
                _logger.Warning("Invalid data source: {DataSource}", action.SetValue.Key);
                errors.Add(new ValidationError($"Invalid data source: {action.SetValue.Key}"));
            }
        }
        // Validate SendMessage action if present
        else if (action.SendMessage != null)
        {
            if (string.IsNullOrEmpty(action.SendMessage.Channel))
            {
                _logger.Warning("Rule '{RuleName}' has an invalid action: missing channel", ruleName);
                errors.Add(
                    new ValidationError($"Rule '{ruleName}' has an invalid action: missing channel")
                );
            }
            if (string.IsNullOrEmpty(action.SendMessage.Message))
            {
                _logger.Warning("Rule '{RuleName}' has an invalid action: missing message", ruleName);
                errors.Add(
                    new ValidationError($"Rule '{ruleName}' has an invalid action: missing message")
                );
            }
        }

        return errors;
    }
}
