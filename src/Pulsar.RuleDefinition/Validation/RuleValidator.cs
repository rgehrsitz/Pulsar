using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.RuleDefinition.Analysis;
using Pulsar.RuleDefinition.Models;
using Serilog;

namespace Pulsar.RuleDefinition.Validation;

public class RuleValidator
{
    private readonly SystemConfig _config;
    private readonly DependencyAnalyzer _dependencyAnalyzer;
    private readonly ILogger _logger;
    private static readonly HashSet<string> ValidOperators = new() { ">", "<", ">=", "<=", "==", "!=" };

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
            return new ValidationResult(errors);
        }

        _logger.Debug("Checking for duplicate rule names in {RuleCount} rules", ruleSet.Rules.Count);
        // Check for duplicate rule names
        var duplicateRules = ruleSet.Rules
            .GroupBy(r => r.Name)
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
                _logger.Warning("Found {ErrorCount} validation errors in rule {RuleName}: {@Errors}", 
                    ruleErrors.Count, rule.Name, ruleErrors);
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

        return new ValidationResult(errors);
    }

    private List<ValidationError> ValidateRule(Rule rule)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrEmpty(rule.Name))
        {
            _logger.Warning("Rule name is empty or null");
            errors.Add(new ValidationError("Rule name cannot be empty"));
        }

        if (rule.Conditions == null)
        {
            _logger.Warning("Rule {RuleName} has no conditions", rule.Name);
            errors.Add(new ValidationError($"Rule {rule.Name}: Conditions cannot be null"));
            return errors;
        }

        // Validate conditions
        if ((rule.Conditions.All == null || !rule.Conditions.All.Any()) &&
            (rule.Conditions.Any == null || !rule.Conditions.Any.Any()))
        {
            _logger.Warning("Rule {RuleName} has no conditions in either 'all' or 'any' groups", rule.Name);
            errors.Add(new ValidationError($"Rule {rule.Name}: Must have at least one condition in 'all' or 'any' group"));
        }

        if (rule.Conditions.All != null)
        {
            foreach (var condition in rule.Conditions.All)
            {
                var conditionErrors = ValidateCondition(condition, rule.Name);
                if (conditionErrors.Any())
                {
                    _logger.Warning("Found {ErrorCount} errors in 'all' condition of rule {RuleName}: {@Errors}", 
                        conditionErrors.Count, rule.Name, conditionErrors);
                    errors.AddRange(conditionErrors);
                }
            }
        }

        if (rule.Conditions.Any != null)
        {
            foreach (var condition in rule.Conditions.Any)
            {
                var conditionErrors = ValidateCondition(condition, rule.Name);
                if (conditionErrors.Any())
                {
                    _logger.Warning("Found {ErrorCount} errors in 'any' condition of rule {RuleName}: {@Errors}", 
                        conditionErrors.Count, rule.Name, conditionErrors);
                    errors.AddRange(conditionErrors);
                }
            }
        }

        // Validate actions
        if (rule.Actions == null || !rule.Actions.Any())
        {
            _logger.Warning("Rule {RuleName} has no actions", rule.Name);
            errors.Add(new ValidationError($"Rule {rule.Name}: Must have at least one action"));
        }
        else
        {
            foreach (var action in rule.Actions)
            {
                var actionErrors = ValidateRuleAction(action, rule.Name);
                if (actionErrors.Any())
                {
                    _logger.Warning("Found {ErrorCount} errors in action of rule {RuleName}: {@Errors}", 
                        actionErrors.Count, rule.Name, actionErrors);
                    errors.AddRange(actionErrors);
                }
            }
        }

        return errors;
    }

    private List<ValidationError> ValidateCondition(Condition condition, string ruleName)
    {
        var errors = new List<ValidationError>();

        if (condition is ComparisonCondition comp)
        {
            if (!_config.ValidSensors.Contains(comp.DataSource))
            {
                _logger.Warning("Invalid data source: {DataSource}", comp.DataSource);
                errors.Add(new ValidationError($"Invalid data source: {comp.DataSource}"));
            }

            if (!ValidOperators.Contains(comp.Operator))
            {
                _logger.Warning("Invalid operator: {Operator}", comp.Operator);
                errors.Add(new ValidationError($"Invalid operator: {comp.Operator}"));
            }
        }
        else if (condition is ThresholdOverTimeCondition threshold)
        {
            if (!_config.ValidSensors.Contains(threshold.DataSource))
            {
                _logger.Warning("Invalid data source: {DataSource}", threshold.DataSource);
                errors.Add(new ValidationError($"Invalid data source: {threshold.DataSource}"));
            }
        }

        return errors;
    }

    private List<ValidationError> ValidateRuleAction(RuleAction action, string ruleName)
    {
        var errors = new List<ValidationError>();

        if (action.SetValue == null || !action.SetValue.Any())
        {
            _logger.Warning("Rule '{RuleName}' has an invalid action: missing set_value", ruleName);
            errors.Add(new ValidationError($"Rule '{ruleName}' has an invalid action: missing set_value"));
        }
        else if (!_config.ValidSensors.Contains(action.SetValue["key"]?.ToString()))
        {
            _logger.Warning("Invalid data source: {DataSource}", action.SetValue["key"]);
            errors.Add(new ValidationError($"Invalid data source: {action.SetValue["key"]}"));
        }

        return errors;
    }
}
