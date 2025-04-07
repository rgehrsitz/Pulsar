// File: Pulsar.Compiler/DslParser.cs

using System.Diagnostics;
using Pulsar.Compiler.Exceptions;
using Pulsar.Compiler.Models;
using Serilog;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Pulsar.Compiler.Parsers
{
    public class DslParser
    {
        private readonly ILogger _logger = LoggingConfig.GetLogger();
        private readonly IDeserializer _deserializer;
        private string _currentFile = string.Empty;

        public DslParser()
        {
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .WithNodeDeserializer(new YamlNodeDeserializer())
                .IgnoreUnmatchedProperties()
                .WithDuplicateKeyChecking() // Add this to catch duplicate keys
                .WithAttemptingUnquotedStringTypeDeserialization() // Add this line
                .Build();
            _logger.Debug("DslParser initialized");
        }

        private class YamlNodeDeserializer : INodeDeserializer
        {
            private static int _recursionDepth = 0;
            private const int MaxRecursionDepth = 100;

            public bool Deserialize(
                IParser parser,
                Type expectedType,
                Func<IParser, Type, object?> nestedObjectDeserializer,
                out object? value,
                ObjectDeserializer rootDeserializer
            )
            {
                value = null;

                // Only handle the top-level Rule; for nested calls, defer to default deserialization.
                if (expectedType != typeof(Rule) || _recursionDepth > 0)
                {
                    return false;
                }

                try
                {
                    _recursionDepth++;
                    if (_recursionDepth > MaxRecursionDepth)
                    {
                        Debug.WriteLine($"Recursion depth exceeded at type: {expectedType.Name}");
                        throw new InvalidOperationException(
                            $"YAML structure is too deeply nested (depth > {MaxRecursionDepth}). Check for circular references in your rules."
                        );
                    }

                    // Deserialize the rule
                    value = nestedObjectDeserializer(parser, expectedType);

                    if (value is Rule rule)
                    {
                        var start = parser.Current?.Start;
                        if (start.HasValue)
                        {
                            rule.LineNumber = (int)start.Value.Line; // Cast long to int

                            // ACTUAL CODE CHANGE: Fix the null reference assignment by using null coalescing operator
                            rule.OriginalText = parser.Current?.ToString() ?? string.Empty;
                        }
                        return true;
                    }
                }
                finally
                {
                    _recursionDepth--;
                }

                return false;
            }
        }

        public List<RuleDefinition> ParseRules(
            string yamlContent,
            List<string> validSensors,
            string fileName = "",
            bool allowInvalidSensors = false
        )
        {
            try
            {
                _currentFile = fileName;
                _logger.Debug("Parsing rules from YAML content");

                if (string.IsNullOrWhiteSpace(yamlContent))
                {
                    throw new ValidationException($"Error parsing YAML: Content is empty");
                }

                // Deserialize YAML
                RuleRoot? root;
                try
                {
                    root = _deserializer.Deserialize<RuleRoot>(yamlContent);
                }
                catch (YamlException ex)
                {
                    throw new ValidationException($"Error parsing YAML: {ex.Message}", ex);
                }

                if (root == null)
                {
                    throw new ValidationException($"Error parsing YAML: Invalid format");
                }

                if (root?.Rules == null || !root.Rules.Any())
                {
                    _logger.Warning("No rules found in YAML content");
                    return new List<RuleDefinition>();
                }

                var ruleDefinitions = new List<RuleDefinition>();

                foreach (var rule in root.Rules)
                {
                    Debug.WriteLine($"\nProcessing rule: {rule.Name}");

                    // Validate sensors and keys
                    ValidateRule(rule, validSensors, allowInvalidSensors);

                    // Show actions debug info
                    if (rule.Actions != null)
                    {
                        foreach (var action in rule.Actions)
                        {
                            if (action?.SetValue != null)
                            {
                                Debug.WriteLine(
                                    $"SetValue action found - Key: {action.SetValue.Key}, Value: {action.SetValue.Value}, Expression: {action.SetValue.ValueExpression}"
                                );
                            }
                        }
                    }

                    // Convert to RuleDefinition
                    var ruleDefinition = new RuleDefinition
                    {
                        Name = rule.Name,
                        Description = rule.Description,
                        Conditions = ConvertConditions(rule.Conditions),
                        Actions = ConvertActions(rule.Actions ?? new List<ActionListItem>()),
                        SourceFile = _currentFile,
                        LineNumber = rule.LineNumber,
                    };

                    ruleDefinitions.Add(ruleDefinition);
                }

                return ruleDefinitions;
            }
            catch (YamlException ex)
            {
                _logger.Error(
                    ex,
                    "YAML parsing error in {FileName}: {Message}",
                    fileName,
                    ex.Message
                );
                throw new ValidationException($"Error parsing YAML: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not ValidationException)
            {
                _logger.Error(
                    ex,
                    "Unexpected error parsing YAML in {FileName}: {Message}",
                    fileName,
                    ex.Message
                );
                throw new ValidationException($"Error parsing YAML: {ex.Message}", ex);
            }
        }

        private void ValidateRule(
            Rule rule,
            IEnumerable<string> validSensors,
            bool allowInvalidSensors = false
        )
        {
            if (string.IsNullOrEmpty(rule.Name))
            {
                throw new ValidationException("Rule name is required");
            }

            if (
                rule.Conditions == null
                || (rule.Conditions.All == null && rule.Conditions.Any == null)
            )
            {
                throw new ValidationException(
                    $"Rule '{rule.Name}' must have at least one condition"
                );
            }

            if (rule.Actions == null || !rule.Actions.Any())
            {
                throw new ValidationException($"Rule '{rule.Name}' must have at least one action");
            }

            if (!allowInvalidSensors)
            {
                ValidateSensors(rule, validSensors.ToList());
            }
            else
            {
                _logger.Information(
                    "Skipping sensor validation for rule {RuleName} (AllowInvalidSensors=true)",
                    rule.Name
                );
            }
        }

        private void ValidateSensors(Rule rule, List<string> validSensors)
        {
            // Ensure validSensors is not null
            if (validSensors == null)
            {
                validSensors = new List<string>();
                _logger.Warning(
                    "ValidSensors was null in ValidateSensors, creating new empty list"
                );
            }

            // Add required sensors if the list is empty or doesn't contain them
            if (validSensors.Count == 0)
            {
                _logger.Warning("ValidSensors list is empty, adding default required sensors");
                validSensors.AddRange(
                    new[] { "temperature_f", "temperature_c", "humidity", "pressure" }
                );
            }
            else
            {
                // Check if required sensors are in the list
                var requiredSensors = new[]
                {
                    "temperature_f",
                    "temperature_c",
                    "humidity",
                    "pressure",
                };
                foreach (var sensor in requiredSensors)
                {
                    if (!validSensors.Contains(sensor))
                    {
                        validSensors.Add(sensor);
                        _logger.Warning(
                            "Added missing required sensor in ValidateSensors: {Sensor}",
                            sensor
                        );
                    }
                }
            }

            _logger.Information(
                "Valid sensors for validation: {ValidSensors}",
                String.Join(", ", validSensors)
            );
            _logger.Debug(
                "Validating sensors for rule: {RuleName}. Valid sensors provided: {ValidSensors}",
                rule.Name,
                String.Join(", ", validSensors)
            );

            var allSensors = new List<string>();

            // Collect sensors from conditions
            if (rule.Conditions?.All != null)
            {
                foreach (var condition in rule.Conditions.All)
                {
                    if (
                        condition.ConditionDetails.Type == "threshold_over_time"
                        || condition.ConditionDetails.Type == "comparison"
                    )
                    {
                        _logger.Information(
                            "Found sensor from condition: {Sensor}",
                            condition.ConditionDetails.Sensor
                        );
                        allSensors.Add(condition.ConditionDetails.Sensor);
                    }
                    else if (condition.ConditionDetails.Type == "expression")
                    {
                        // For expressions, we'll validate the expression syntax separately
                        _logger.Debug(
                            $"Expression condition: {condition.ConditionDetails.Expression}"
                        );
                    }
                }
            }

            if (rule.Conditions?.Any != null)
            {
                foreach (var condition in rule.Conditions.Any)
                {
                    if (
                        condition.ConditionDetails.Type == "threshold_over_time"
                        || condition.ConditionDetails.Type == "comparison"
                    )
                    {
                        _logger.Information(
                            "Found sensor from condition: {Sensor}",
                            condition.ConditionDetails.Sensor
                        );
                        allSensors.Add(condition.ConditionDetails.Sensor);
                    }
                    else if (condition.ConditionDetails.Type == "expression")
                    {
                        _logger.Debug(
                            $"Expression condition: {condition.ConditionDetails.Expression}"
                        );
                    }
                }
            }

            // Validate sensors against the valid list
            var invalidSensors = allSensors
                .Where(sensor => !validSensors.Contains(sensor))
                .Distinct()
                .ToList();

            if (invalidSensors.Any())
            {
                _logger.Error(
                    "Invalid sensors found: {InvalidSensors}",
                    String.Join(", ", invalidSensors)
                );
                throw new ValidationException(
                    $"Invalid sensors found: {String.Join(", ", invalidSensors)}"
                );
            }

            // Note: We don't validate action keys as they are outputs, not inputs
        }

        private IEnumerable<string> GetSensorsFromConditions(List<Condition> conditions)
        {
            foreach (var condition in conditions)
            {
                if (condition.ConditionDetails?.Sensor != null)
                    yield return condition.ConditionDetails.Sensor;
            }
        }

        private ConditionGroup ConvertConditions(ConditionGroupYaml? conditionGroupYaml)
        {
            // Ensure conditionGroupYaml is not null
            conditionGroupYaml ??= new ConditionGroupYaml();

            // Default to empty lists if null
            var allConditions = conditionGroupYaml.All ?? new List<Condition>();
            var anyConditions = conditionGroupYaml.Any ?? new List<Condition>();

            // Perform conversions
            return new ConditionGroup
            {
                All = allConditions.Select(ConvertCondition).ToList(),
                Any = anyConditions.Select(ConvertCondition).ToList(),
            };
        }

        private ConditionDefinition ConvertCondition(Condition condition)
        {
            if (condition.ConditionDetails == null)
            {
                throw new ValidationException("Condition details cannot be null");
            }

            switch (condition.ConditionDetails.Type.ToLowerInvariant())
            {
                case "comparison":
                    if (string.IsNullOrEmpty(condition.ConditionDetails.Sensor))
                    {
                        throw new ValidationException("Comparison condition must specify a sensor");
                    }
                    return new ComparisonCondition
                    {
                        Type = ConditionType.Comparison,
                        Sensor = condition.ConditionDetails.Sensor,
                        Operator = ParseOperator(condition.ConditionDetails.Operator),
                        Value = condition.ConditionDetails.Value,
                    };

                case "expression":
                    if (string.IsNullOrEmpty(condition.ConditionDetails.Expression))
                    {
                        throw new ValidationException(
                            "Expression condition must specify an expression"
                        );
                    }
                    return new ExpressionCondition
                    {
                        Type = ConditionType.Expression,
                        Expression = condition.ConditionDetails.Expression,
                    };

                case "threshold_over_time":
                    if (string.IsNullOrEmpty(condition.ConditionDetails.Sensor))
                    {
                        throw new ValidationException(
                            "Threshold over time condition must specify a sensor"
                        );
                    }
                    if (condition.ConditionDetails.Duration <= 0)
                    {
                        throw new ValidationException(
                            "Threshold over time condition must specify a positive duration"
                        );
                    }
                    
                    // Convert the threshold value properly
                    double thresholdValue = 0;
                    if (condition.ConditionDetails.Threshold > 0)
                    {
                        thresholdValue = condition.ConditionDetails.Threshold;
                    }
                    else if (condition.ConditionDetails.Value != null)
                    {
                        // Try to convert Value to double for threshold
                        if (condition.ConditionDetails.Value is double dVal)
                        {
                            thresholdValue = dVal;
                        }
                        else if (condition.ConditionDetails.Value is int iVal)
                        {
                            thresholdValue = iVal;
                        }
                        else if (condition.ConditionDetails.Value is string sVal && double.TryParse(sVal, out double parsedVal))
                        {
                            thresholdValue = parsedVal;
                        }
                        else
                        {
                            throw new ValidationException(
                                "Threshold over time condition must specify a numeric threshold"
                            );
                        }
                    }
                    else
                    {
                        throw new ValidationException(
                            "Threshold over time condition must specify a positive threshold"
                        );
                    }

                    var mode = ThresholdOverTimeMode.Strict; // Default to strict mode
                    if (!string.IsNullOrEmpty(condition.ConditionDetails.Mode))
                    {
                        mode = condition.ConditionDetails.Mode.ToLowerInvariant() switch
                        {
                            "strict" => ThresholdOverTimeMode.Strict,
                            "extended" => ThresholdOverTimeMode.Extended,
                            _ => throw new ValidationException(
                                $"Invalid temporal mode: {condition.ConditionDetails.Mode}. Must be 'strict' or 'extended'."
                            ),
                        };
                    }

                    return new ThresholdOverTimeCondition
                    {
                        Type = ConditionType.ThresholdOverTime,
                        Sensor = condition.ConditionDetails.Sensor,
                        Threshold = thresholdValue,
                        Duration = condition.ConditionDetails.Duration,
                        Mode = mode,
                    };

                case "group":
                    var group = new ConditionGroup { Type = ConditionType.Group };

                    if (condition.ConditionDetails.All != null)
                    {
                        foreach (var subCondition in condition.ConditionDetails.All)
                        {
                            group.AddToAll(ConvertCondition(subCondition));
                        }
                    }

                    if (condition.ConditionDetails.Any != null)
                    {
                        foreach (var subCondition in condition.ConditionDetails.Any)
                        {
                            group.AddToAny(ConvertCondition(subCondition));
                        }
                    }

                    if (
                        (group.All == null || !group.All.Any())
                        && (group.Any == null || !group.Any.Any())
                    )
                    {
                        throw new ValidationException(
                            "Group condition must have at least one condition in All or Any"
                        );
                    }

                    return group;

                default:
                    throw new ValidationException(
                        $"Unsupported condition type: {condition.ConditionDetails.Type}"
                    );
            }
        }

        private ComparisonOperator ParseOperator(string op)
        {
            return op.ToLowerInvariant() switch
            {
                "greater_than" or "gt" or ">" => ComparisonOperator.GreaterThan,
                "less_than" or "lt" or "<" => ComparisonOperator.LessThan,
                "greater_than_or_equal" or "gte" or ">=" => ComparisonOperator.GreaterThanOrEqual,
                "less_than_or_equal" or "lte" or "<=" => ComparisonOperator.LessThanOrEqual,
                "equal_to" or "eq" or "==" => ComparisonOperator.EqualTo,
                "not_equal_to" or "ne" or "!=" => ComparisonOperator.NotEqualTo,
                _ => throw new ValidationException(
                    $"Invalid operator: {op}. Must be one of: greater_than, less_than, greater_than_or_equal, less_than_or_equal, equal_to, not_equal_to"
                ),
            };
        }

        private List<ActionDefinition> ConvertActions(List<ActionListItem> actions)
        {
            Debug.WriteLine("=== Starting ConvertActions ===");
            Debug.WriteLine($"Number of actions: {actions.Count}");
            foreach (var actionItem in actions)
            {
                Debug.WriteLine($"Action item details:");
                Debug.WriteLine($"  Item is null?: {actionItem == null}");
                Debug.WriteLine($"  SetValue is null?: {actionItem?.SetValue == null}");
                Debug.WriteLine($"  SendMessage is null?: {actionItem?.SendMessage == null}");

                if (actionItem?.SetValue != null)
                {
                    Debug.WriteLine($"  SetValue.Key: {actionItem.SetValue.Key}");
                    Debug.WriteLine($"  SetValue.Value: {actionItem.SetValue.Value}");
                    Debug.WriteLine(
                        $"  SetValue.ValueExpression: {actionItem.SetValue.ValueExpression}"
                    );
                }
            }

            return actions
                .Select<ActionListItem, ActionDefinition>(actionItem =>
                {
                    Debug.WriteLine($"Processing action item");

                    if (actionItem?.SetValue != null)
                    {
                        Debug.WriteLine($"Found SetValue action");
                        if (string.IsNullOrEmpty(actionItem.SetValue.Key))
                        {
                            throw new ValidationException("SetValue action must have a key");
                        }

                        var setValueAction = new SetValueAction
                        {
                            Type = ActionType.SetValue,
                            Key = actionItem.SetValue.Key,
                            Value = actionItem.SetValue.Value,
                            ValueExpression = actionItem.SetValue.ValueExpression,
                        };
                        Debug.WriteLine(
                            $"Created SetValueAction - Key: {setValueAction.Key}, Value: {setValueAction.Value}, Expression: {setValueAction.ValueExpression}"
                        );
                        return setValueAction;
                    }

                    if (actionItem?.SendMessage != null)
                    {
                        Debug.WriteLine($"Found SendMessage action");
                        if (string.IsNullOrEmpty(actionItem.SendMessage.Channel))
                        {
                            throw new ValidationException("SendMessage action must have a channel");
                        }

                        return new SendMessageAction
                        {
                            Type = ActionType.SendMessage,
                            Channel = actionItem.SendMessage.Channel,
                            Message = actionItem.SendMessage.Message,
                            MessageExpression = actionItem.SendMessage.MessageExpression,
                        };
                    }

                    Debug.WriteLine($"No valid action type found");
                    throw new ValidationException(
                        $"Unknown action type. Action item details: {actionItem?.ToString() ?? "null"}"
                    );
                })
                .ToList();
        }
    }

    // Public classes for YAML Parsing
    public class RuleRoot
    {
        public List<Rule> Rules { get; set; } = new();
    }

    public class Rule
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ConditionGroupYaml? Conditions { get; set; }
        public List<ActionListItem>? Actions { get; set; }

        // Line tracking properties
        public int LineNumber { get; set; }
        public string? OriginalText { get; set; }
    }

    public class ActionListItem
    {
        [YamlMember(Alias = "set_value")]
        public SetValueActionYaml? SetValue { get; set; }

        [YamlMember(Alias = "send_message")]
        public SendMessageActionYaml? SendMessage { get; set; }
    }

    public class ConditionGroupYaml
    {
        public List<Condition> All { get; set; } = new();
        public List<Condition> Any { get; set; } = new();
    }

    public class Condition
    {
        [YamlMember(Alias = "condition")]
        public ConditionInner ConditionDetails { get; set; } = new();
    }

    public class ConditionInner
    {
        public string Type { get; set; } = string.Empty;
        public string Sensor { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public object? Value { get; set; }  // Changed from double to object? to support boolean values
        public double Threshold { get; set; }
        public string Expression { get; set; } = string.Empty;
        public int Duration { get; set; }
        public string Mode { get; set; } = string.Empty;
        public List<Condition> All { get; set; } = new();
        public List<Condition> Any { get; set; } = new();
    }

    public class ActionYaml
    {
        [YamlMember(Alias = "set_value")]
        public SetValueActionYaml? SetValue { get; set; }

        [YamlMember(Alias = "send_message")]
        public SendMessageActionYaml? SendMessage { get; set; }
    }

    public class SetValueActionYaml
    {
        [YamlMember(Alias = "key")]
        public string Key { get; set; } = string.Empty;

        [YamlMember(Alias = "value")]
        public object? Value { get; set; }

        [YamlMember(Alias = "value_expression")]
        public string? ValueExpression { get; set; }
    }

    public class SendMessageActionYaml
    {
        [YamlMember(Alias = "channel")]
        public string Channel { get; set; } = string.Empty;

        [YamlMember(Alias = "message")]
        public string Message { get; set; } = string.Empty;
        
        [YamlMember(Alias = "message_expression")]
        public string? MessageExpression { get; set; }
    }
}
