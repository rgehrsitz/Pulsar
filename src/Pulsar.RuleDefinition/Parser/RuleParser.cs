using System;
using System.IO;
using System.Linq;
using Pulsar.RuleDefinition.Models;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Pulsar.RuleDefinition.Parser;

/// <summary>
/// Provides functionality to parse rule definition YAML files into strongly-typed objects
/// </summary>
public class RuleParser
{
    private readonly IDeserializer _deserializer;
    private readonly SystemConfig _config;

    public RuleParser(SystemConfig config)
    {
        _config = config;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new ConditionTypeConverter())
            .WithTypeConverter(new ActionTypeConverter())
            .Build();
    }

    /// <summary>
    /// Parses a YAML string containing rule definitions into a RuleSetDefinition object
    /// </summary>
    /// <param name="yamlContent">The YAML content to parse</param>
    /// <returns>A RuleSetDefinition object representing the parsed rules</returns>
    /// <exception cref="ArgumentException">If the YAML is invalid or missing required fields</exception>
    public RuleSetDefinition ParseRules(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            throw new ArgumentException("YAML content cannot be empty");
        }

        try
        {
            var result = _deserializer.Deserialize<RuleSetDefinition>(yamlContent);
            if (result == null)
            {
                throw new ArgumentException(
                    "Invalid YAML: failed to deserialize to RuleSetDefinition"
                );
            }

            // Validate required fields
            if (result.Rules == null || !result.Rules.Any())
            {
                throw new ArgumentException("Rules section is missing or empty");
            }

            // Validate each rule
            foreach (var rule in result.Rules)
            {
                ValidateRule(rule);
            }

            return result;
        }
        catch (YamlException ex)
        {
            throw new ArgumentException($"Invalid YAML format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses a YAML file containing rule definitions into a RuleSetDefinition object
    /// </summary>
    /// <param name="filePath">Path to the YAML file</param>
    /// <returns>A RuleSetDefinition object representing the parsed rules</returns>
    public RuleSetDefinition ParseRulesFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Rule file not found: {filePath}");
        }

        var yamlContent = File.ReadAllText(filePath);
        return ParseRules(yamlContent);
    }

    private void ValidateRule(Rule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            throw new ArgumentException("Rule name is required");
        }

        if (rule.Conditions == null)
        {
            throw new ArgumentException($"Rule '{rule.Name}' is missing conditions");
        }

        if (rule.Actions == null || !rule.Actions.Any())
        {
            throw new ArgumentException($"Rule '{rule.Name}' is missing actions");
        }

        // Validate conditions
        ValidateConditionGroup(rule.Conditions, rule.Name);

        // Validate actions
        foreach (var action in rule.Actions)
        {
            ValidateAction(action, rule.Name);
        }
    }

    private void ValidateConditionGroup(ConditionGroup group, string ruleName)
    {
        if ((group.All == null || !group.All.Any()) && (group.Any == null || !group.Any.Any()))
        {
            throw new ArgumentException($"Rule '{ruleName}' has no conditions in group");
        }

        if (group.All != null && group.Any != null)
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' cannot have both 'all' and 'any' conditions in the same group"
            );
        }

        var conditions = group.All ?? group.Any;
        foreach (var condition in conditions!)
        {
            ValidateCondition(condition, ruleName);
        }
    }

    private void ValidateCondition(Condition condition, string ruleName)
    {
        switch (condition)
        {
            case ComparisonCondition comp:
                ValidateComparisonCondition(comp, ruleName);
                break;
            case ThresholdOverTimeCondition threshold:
                ValidateThresholdOverTimeCondition(threshold, ruleName);
                break;
            case ExpressionCondition expr:
                ValidateExpressionCondition(expr, ruleName);
                break;
            default:
                throw new ArgumentException(
                    $"Rule '{ruleName}' has unknown condition type: {condition.GetType().Name}"
                );
        }
    }

    private void ValidateComparisonCondition(ComparisonCondition condition, string ruleName)
    {
        if (string.IsNullOrWhiteSpace(condition.DataSource))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' has comparison condition with missing sensor"
            );
        }

        if (!_config.ValidSensors.Contains(condition.DataSource))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' references invalid sensor: {condition.DataSource}"
            );
        }

        if (string.IsNullOrWhiteSpace(condition.Operator))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' has comparison condition with missing operator"
            );
        }

        if (!IsValidOperator(condition.Operator))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' has invalid comparison operator: {condition.Operator}"
            );
        }
    }

    private void ValidateThresholdOverTimeCondition(
        ThresholdOverTimeCondition condition,
        string ruleName
    )
    {
        if (string.IsNullOrWhiteSpace(condition.DataSource))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' has threshold condition with missing sensor"
            );
        }

        if (!_config.ValidSensors.Contains(condition.DataSource))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' references invalid sensor: {condition.DataSource}"
            );
        }

        if (condition.DurationMs <= 0)
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' has threshold condition with invalid duration: {condition.DurationMs}ms"
            );
        }
    }

    private void ValidateExpressionCondition(ExpressionCondition condition, string ruleName)
    {
        if (string.IsNullOrWhiteSpace(condition.Expression))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' has expression condition with missing expression"
            );
        }

        // TODO: Add expression validation
        // This would validate:
        // 1. Expression syntax
        // 2. Referenced sensors exist in _config.ValidSensors
        // 3. Operators and functions are valid
    }

    private void ValidateAction(RuleAction action, string ruleName)
    {
        if (action.SetValue != null)
        {
            ValidateSetValueAction(action.SetValue, ruleName);
        }
        else if (action.SendMessage != null)
        {
            ValidateSendMessageAction(action.SendMessage, ruleName);
        }
        else
        {
            throw new ArgumentException($"Rule '{ruleName}' has invalid action type");
        }
    }

    private void ValidateSetValueAction(Models.Actions.SetValueAction action, string ruleName)
    {
        if (string.IsNullOrWhiteSpace(action.Key))
        {
            throw new ArgumentException($"Rule '{ruleName}' has set_value action with missing key");
        }

        if (!_config.ValidSensors.Contains(action.Key))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' tries to set invalid sensor: {action.Key}"
            );
        }

        if (action.Value == null && string.IsNullOrWhiteSpace(action.ValueExpression))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' has set_value action with neither value nor value_expression"
            );
        }

        if (action.Value != null && !string.IsNullOrWhiteSpace(action.ValueExpression))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' has set_value action with both value and value_expression"
            );
        }

        // TODO: Add expression validation for value_expression
    }

    private void ValidateSendMessageAction(Models.Actions.SendMessageAction action, string ruleName)
    {
        if (string.IsNullOrWhiteSpace(action.Channel))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' has send_message action with missing channel"
            );
        }

        if (string.IsNullOrWhiteSpace(action.Message))
        {
            throw new ArgumentException(
                $"Rule '{ruleName}' has send_message action with missing message"
            );
        }
    }

    private bool IsValidOperator(string op)
    {
        return op switch
        {
            ">" or ">=" or "<" or "<=" or "==" or "!=" => true,
            _ => false
        };
    }
}
