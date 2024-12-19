using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Analysis;

namespace Pulsar.RuleDefinition.Validation;

/// <summary>
/// Validates rule definitions to ensure they meet all requirements
/// </summary>
public class RuleValidator
{
    private readonly ExpressionValidator _expressionValidator;
    private readonly TemporalValidator _temporalValidator;
    private readonly DependencyAnalyzer _dependencyAnalyzer;

    public RuleValidator()
    {
        _expressionValidator = new ExpressionValidator();
        _temporalValidator = new TemporalValidator();
        _dependencyAnalyzer = new DependencyAnalyzer();
    }

    /// <summary>
    /// Validates a complete rule set definition and analyzes dependencies
    /// </summary>
    /// <returns>Validation result containing errors and ordered rules if validation succeeds</returns>
    public ValidationResult ValidateRuleSet(RuleSetDefinition ruleSet)
    {
        var result = new ValidationResult();

        // Basic validation
        if (ruleSet.Version <= 0)
        {
            result.Errors.Add(new ValidationError("Version must be a positive integer"));
        }

        if (ruleSet.ValidDataSources == null || !ruleSet.ValidDataSources.Any())
        {
            result.Errors.Add(new ValidationError("At least one valid data source must be defined"));
        }
        else
        {
            // Check for duplicate data sources
            var duplicates = ruleSet.ValidDataSources
                .GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var duplicate in duplicates)
            {
                result.Errors.Add(new ValidationError($"Duplicate data source found: {duplicate}"));
            }
        }

        // Validate rules
        if (ruleSet.Rules == null || !ruleSet.Rules.Any())
        {
            result.Errors.Add(new ValidationError("At least one rule must be defined"));
        }
        else
        {
            // Validate each rule
            foreach (var rule in ruleSet.Rules)
            {
                result.Errors.AddRange(ValidateRule(rule, ruleSet.ValidDataSources));
            }

            // Check for duplicate rule names
            var duplicateRules = ruleSet.Rules
                .GroupBy(r => r.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var duplicate in duplicateRules)
            {
                result.Errors.Add(new ValidationError($"Duplicate rule name found: {duplicate}"));
            }

            // Analyze dependencies if no validation errors
            if (!result.Errors.Any())
            {
                var (orderedRules, cyclicDependencies) = _dependencyAnalyzer.AnalyzeAndOrder(ruleSet);
                
                if (cyclicDependencies.Any())
                {
                    result.Errors.AddRange(cyclicDependencies.Select(cd => new ValidationError(cd)));
                }
                else
                {
                    result.OrderedRules = orderedRules;
                }
            }
        }

        return result;
    }

    private List<ValidationError> ValidateRule(Rule rule, List<string> validDataSources)
    {
        var errors = new List<ValidationError>();

        // Validate rule name
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            errors.Add(new ValidationError("Rule name cannot be empty"));
        }

        // Validate conditions
        if (rule.Conditions == null)
        {
            errors.Add(new ValidationError($"Rule '{rule.Name}' must have conditions defined"));
        }
        else
        {
            errors.AddRange(ValidateConditionGroup(rule.Name, rule.Conditions, validDataSources));
        }

        // Validate actions
        if (rule.Actions == null || !rule.Actions.Any())
        {
            errors.Add(new ValidationError($"Rule '{rule.Name}' must have at least one action defined"));
        }
        else
        {
            foreach (var action in rule.Actions)
            {
                errors.AddRange(ValidateAction(rule.Name, action));
            }
        }

        return errors;
    }

    private List<ValidationError> ValidateConditionGroup(string ruleName, ConditionGroup group, List<string> validDataSources)
    {
        var errors = new List<ValidationError>();

        if ((group.All == null || !group.All.Any()) &&
            (group.Any == null || !group.Any.Any()))
        {
            errors.Add(new ValidationError($"Rule '{ruleName}' must have at least one condition in 'all' or 'any' group"));
        }

        // Validate conditions in 'all' group
        if (group.All != null)
        {
            foreach (var condition in group.All)
            {
                errors.AddRange(ValidateCondition(ruleName, condition, validDataSources));
            }
        }

        // Validate conditions in 'any' group
        if (group.Any != null)
        {
            foreach (var condition in group.Any)
            {
                errors.AddRange(ValidateCondition(ruleName, condition, validDataSources));
            }
        }

        return errors;
    }

    private List<ValidationError> ValidateCondition(string ruleName, Condition condition, List<string> validDataSources)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(condition.Type))
        {
            errors.Add(new ValidationError($"Rule '{ruleName}' has a condition with no type specified"));
            return errors;
        }

        switch (condition)
        {
            case ComparisonCondition comp:
                if (!validDataSources.Contains(comp.DataSource))
                {
                    errors.Add(new ValidationError($"Rule '{ruleName}' references invalid data source: {comp.DataSource}"));
                }
                if (!new[] { "<", ">", "<=", ">=", "==", "!=" }.Contains(comp.Operator))
                {
                    errors.Add(new ValidationError($"Rule '{ruleName}' has invalid comparison operator: {comp.Operator}"));
                }
                break;

            case ThresholdOverTimeCondition threshold:
                if (!validDataSources.Contains(threshold.DataSource))
                {
                    errors.Add(new ValidationError($"Rule '{ruleName}' references invalid data source: {threshold.DataSource}"));
                }
                errors.AddRange(
                    _temporalValidator.ValidateTemporalCondition(threshold)
                        .Select(e => new ValidationError($"Rule '{ruleName}': {e}"))
                );
                break;

            case ExpressionCondition expr:
                var (isValid, dataSources, expressionErrors) = _expressionValidator.ValidateExpression(expr.Expression);
                if (!isValid)
                {
                    errors.AddRange(expressionErrors.Select(e => new ValidationError($"Rule '{ruleName}': {e}")));
                }
                else
                {
                    // Validate that all referenced data sources are valid
                    var invalidSources = dataSources.Where(ds => !validDataSources.Contains(ds));
                    foreach (var invalidSource in invalidSources)
                    {
                        errors.Add(new ValidationError($"Rule '{ruleName}' references invalid data source in expression: {invalidSource}"));
                    }
                }
                break;
        }

        return errors;
    }

    private List<ValidationError> ValidateAction(string ruleName, RuleAction action)
    {
        var errors = new List<ValidationError>();

        if (action.SetValue != null && action.SetValue.Any())
        {
            if (!action.SetValue.ContainsKey("key") || string.IsNullOrWhiteSpace(action.SetValue["key"]?.ToString()))
            {
                errors.Add(new ValidationError($"Rule '{ruleName}' has set_value action with missing or empty key"));
            }
            if (!action.SetValue.ContainsKey("value"))
            {
                errors.Add(new ValidationError($"Rule '{ruleName}' has set_value action with missing value"));
            }
        }
        else if (action.SendMessage != null && action.SendMessage.Any())
        {
            if (!action.SendMessage.ContainsKey("channel") || string.IsNullOrWhiteSpace(action.SendMessage["channel"]))
            {
                errors.Add(new ValidationError($"Rule '{ruleName}' has send_message action with missing or empty channel"));
            }
            if (!action.SendMessage.ContainsKey("message") || string.IsNullOrWhiteSpace(action.SendMessage["message"]))
            {
                errors.Add(new ValidationError($"Rule '{ruleName}' has send_message action with missing or empty message"));
            }
        }
        else
        {
            errors.Add(new ValidationError($"Rule '{ruleName}' has invalid action type"));
        }

        return errors;
    }
}

/// <summary>
/// Result of rule validation, including any errors and ordered rules if validation succeeds
/// </summary>
public class ValidationResult
{
    public List<ValidationError> Errors { get; set; } = new();
    public List<Rule>? OrderedRules { get; set; }
    public bool IsValid => !Errors.Any();
}

/// <summary>
/// Represents a validation error in the rule definitions
/// </summary>
public class ValidationError
{
    public string Message { get; }

    public ValidationError(string message)
    {
        Message = message;
    }

    public override string ToString() => Message;
}
