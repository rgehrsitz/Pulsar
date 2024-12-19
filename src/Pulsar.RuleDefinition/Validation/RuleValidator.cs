using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.RuleDefinition.Models;

namespace Pulsar.RuleDefinition.Validation;

/// <summary>
/// Validates rule definitions against system configuration
/// </summary>
public class RuleValidator
{
    private readonly SystemConfig _systemConfig;
    private static readonly HashSet<string> ValidOperators = new() { ">", "<", ">=", "<=", "==", "!=" };

    public RuleValidator(SystemConfig systemConfig)
    {
        _systemConfig = systemConfig ?? throw new ArgumentNullException(nameof(systemConfig));
    }

    /// <summary>
    /// Validates a ruleset against the system configuration
    /// </summary>
    /// <param name="ruleSet">The ruleset to validate</param>
    /// <returns>Validation result containing any errors</returns>
    public ValidationResult ValidateRuleSet(RuleSetDefinition ruleSet)
    {
        var errors = new List<ValidationError>();

        if (ruleSet == null)
            throw new ArgumentNullException(nameof(ruleSet));

        if (ruleSet.Version <= 0)
            errors.Add(new ValidationError("Version must be greater than 0"));

        if (ruleSet.Rules == null || !ruleSet.Rules.Any())
        {
            errors.Add(new ValidationError("Rules list cannot be empty"));
            return new ValidationResult(errors);
        }

        // Check for duplicate rule names
        var duplicateRules = ruleSet.Rules
            .GroupBy(r => r.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicateRules)
        {
            errors.Add(new ValidationError($"Duplicate rule name: {duplicate}"));
        }

        // Validate each rule
        foreach (var rule in ruleSet.Rules)
        {
            errors.AddRange(ValidateRule(rule));
        }

        return new ValidationResult(errors);
    }

    /// <summary>
    /// Validates a single rule
    /// </summary>
    /// <param name="rule">The rule to validate</param>
    /// <returns>List of validation errors</returns>
    private List<ValidationError> ValidateRule(Rule rule)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            errors.Add(new ValidationError("Rule name cannot be empty"));
            return errors;
        }

        // Validate conditions
        if (rule.Conditions?.All == null && rule.Conditions?.Any == null)
        {
            errors.Add(new ValidationError($"Must have at least one condition"));
        }
        else
        {
            if (rule.Conditions.All != null)
                errors.AddRange(ValidateConditions(rule.Name, rule.Conditions.All));
            if (rule.Conditions.Any != null)
                errors.AddRange(ValidateConditions(rule.Name, rule.Conditions.Any));
        }

        // Validate actions
        if (rule.Actions == null || !rule.Actions.Any())
        {
            errors.Add(new ValidationError($"Must have at least one action"));
        }
        else
        {
            foreach (var action in rule.Actions)
            {
                if (action.SetValue == null || !action.SetValue.Any())
                {
                    errors.Add(new ValidationError($"Rule '{rule.Name}' has an invalid action: missing set_value"));
                }
                else if (!_systemConfig.ValidSensors.Contains(action.SetValue["key"]?.ToString()))
                {
                    errors.Add(new ValidationError($"Invalid data source: {action.SetValue["key"]}"));
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates a list of conditions
    /// </summary>
    /// <param name="ruleName">Name of the rule containing the conditions</param>
    /// <param name="conditions">List of conditions to validate</param>
    /// <returns>List of validation errors</returns>
    private List<ValidationError> ValidateConditions(string ruleName, List<Condition> conditions)
    {
        var errors = new List<ValidationError>();

        foreach (var condition in conditions)
        {
            if (condition is ComparisonCondition comp)
            {
                if (!_systemConfig.ValidSensors.Contains(comp.DataSource))
                {
                    errors.Add(new ValidationError($"Invalid data source: {comp.DataSource}"));
                }

                if (!ValidOperators.Contains(comp.Operator))
                {
                    errors.Add(new ValidationError($"Invalid operator: {comp.Operator}"));
                }
            }
            else if (condition is ThresholdOverTimeCondition threshold)
            {
                if (!_systemConfig.ValidSensors.Contains(threshold.DataSource))
                {
                    errors.Add(new ValidationError($"Invalid data source: {threshold.DataSource}"));
                }
            }
        }

        return errors;
    }
}
