// File: Pulsar.Compiler/DslParser.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Pulsar.Compiler.Exceptions;
using Pulsar.Compiler.Models;
using Serilog;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

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
                            rule.OriginalText = parser.Current?.ToString();
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
            string fileName = ""
        )
        {
            try
            {
                _currentFile = fileName;
                var root = _deserializer.Deserialize<RuleRoot>(yamlContent);

                if (root?.Rules == null || !root.Rules.Any())
                {
                    throw new InvalidOperationException($"No rules found in file: {fileName}");
                }

                Debug.WriteLine($"\nParsed YAML root: Rules count = {root?.Rules?.Count ?? 0}");

                // Validate that rules are not empty
                if (root?.Rules == null || !root.Rules.Any())
                {
                    throw new InvalidOperationException(
                        "The YAML file is invalid: no rules found."
                    );
                }

                if (root?.Rules?.Any() == true)
                {
                    var firstRule = root.Rules.First();
                    Debug.WriteLine(
                        $"First rule: Name = {firstRule.Name}, Actions count = {firstRule.Actions?.Count ?? 0}"
                    );
                }

                var ruleDefinitions = new List<RuleDefinition>();

                if (root?.Rules == null)
                {
                    return ruleDefinitions;
                }

                foreach (var rule in root.Rules)
                {
                    Debug.WriteLine($"\nProcessing rule: {rule.Name}");

                    // Validate sensors and keys
                    ValidateRule(rule, validSensors);

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
                        LineNumber = rule.LineNumber
                    };

                    ruleDefinitions.Add(ruleDefinition);
                }

                return ruleDefinitions;
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                throw new InvalidOperationException(
                    $"Error parsing YAML in {fileName}: {ex.Message}",
                    ex
                );
            }
        }

        private void ValidateRule(Rule rule, IEnumerable<string> validSensors)
        {
            if (string.IsNullOrEmpty(rule.Name))
            {
                throw new ValidationException("Rule name cannot be empty");
            }

            // Validate that rule has at least one condition
            if (
                rule.Conditions == null
                || (
                    (rule.Conditions.All == null || rule.Conditions.All.Count == 0)
                    && (rule.Conditions.Any == null || rule.Conditions.Any.Count == 0)
                )
            )
            {
                throw new ValidationException(
                    $"Rule '{rule.Name}' must have at least one condition"
                );
            }

            ValidateSensors(rule, validSensors.ToList());
        }

        private void ValidateSensors(Rule rule, List<string> validSensors)
        {
            Log.Information(
                "[DslParser] Validating sensors for rule: {RuleName}. Valid sensors provided: {ValidSensors}",
                rule.Name,
                String.Join(", ", validSensors)
            );
            var conditionSensors =
                rule.Conditions?.All != null
                    ? GetSensorsFromConditions(rule.Conditions.All).ToList()
                    : new List<string>();
            Log.Information(
                "[DslParser] Sensors from conditions: {ConditionSensors}",
                String.Join(", ", conditionSensors)
            ); // Fixed: string.join -> String.Join
            var actionKeys =
                rule.Actions != null
                    ? rule
                        .Actions.Select(a => a.SetValue?.Key)
                        .Where(k => k != null)
                        .Select(k => k!)
                        .ToList()
                    : new List<string>(); // Changed to explicitly check for null
            Log.Information("[DslParser] Action keys: {ActionKeys}", String.Join(", ", actionKeys));

            Debug.WriteLine($"\nValidating rule: {rule.Name}");
            Debug.WriteLine($"Valid sensors list: {String.Join(", ", validSensors)}");

            var allSensors = new List<string>();

            // Collect sensors from conditions
            if (rule.Conditions?.All != null)
            {
                foreach (var condition in rule.Conditions.All)
                {
                    Debug.WriteLine(
                        $"Processing All condition: Type = {condition.ConditionDetails.Type}"
                    );
                    if (
                        condition.ConditionDetails.Type == "threshold_over_time"
                        || condition.ConditionDetails.Type == "comparison"
                    )
                    {
                        Debug.WriteLine($"Adding sensor: {condition.ConditionDetails.Sensor}");
                        allSensors.Add(condition.ConditionDetails.Sensor);
                    }
                    else if (condition.ConditionDetails.Type == "expression")
                    {
                        _logger.Debug(
                            $"Expression condition: {condition.ConditionDetails.Expression}"
                        );
                        // We might need to parse the expression to extract sensors
                    }
                }
            }

            if (rule.Conditions?.Any != null)
            {
                foreach (var condition in rule.Conditions.Any)
                {
                    Debug.WriteLine(
                        $"Processing Any condition: Type = {condition.ConditionDetails.Type}"
                    );
                    if (
                        condition.ConditionDetails.Type == "threshold_over_time"
                        || condition.ConditionDetails.Type == "comparison"
                    )
                    {
                        Debug.WriteLine($"Adding sensor: {condition.ConditionDetails.Sensor}");
                        allSensors.Add(condition.ConditionDetails.Sensor);
                    }
                    else if (condition.ConditionDetails.Type == "expression")
                    {
                        _logger.Debug(
                            $"Expression condition: {condition.ConditionDetails.Expression}"
                        );
                        // We might need to parse the expression to extract sensors
                    }
                }
            }

            // Collect sensors from actions
            if (rule.Actions != null)
            {
                foreach (var actionItem in rule.Actions)
                {
                    Debug.WriteLine("Processing action:");
                    if (actionItem != null && actionItem.SetValue != null)
                    {
                        Debug.WriteLine($"Adding SetValue key: {actionItem.SetValue.Key}");
                        allSensors.Add(actionItem.SetValue.Key);
                    }
                    if (actionItem != null && actionItem.SendMessage != null)
                    {
                        Debug.WriteLine($"SendMessage channel: {actionItem.SendMessage.Channel}");
                        // Do we need to validate the channel?
                    }
                }
            }

            Debug.WriteLine($"All collected sensors: {String.Join(", ", allSensors)}");

            // Validate sensors against the valid list
            var invalidSensors = allSensors
                .Where(sensor => !validSensors.Contains(sensor))
                .ToList();

            Debug.WriteLine($"Invalid sensors found: {String.Join(", ", invalidSensors)}");

            if (invalidSensors.Any())
                throw new InvalidOperationException(
                    $"Invalid sensors or keys found: {String.Join(", ", invalidSensors)}"
                );
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
            switch (condition.ConditionDetails.Type)
            {
                case "comparison":
                    return new ComparisonCondition
                    {
                        Sensor = condition.ConditionDetails.Sensor,
                        Operator = ParseOperator(condition.ConditionDetails.Operator),
                        Value = condition.ConditionDetails.Value,
                    };
                case "expression":
                    return new ExpressionCondition
                    {
                        Expression = condition.ConditionDetails.Expression,
                    };
                case "threshold_over_time":
                    return new ThresholdOverTimeCondition
                    {
                        Sensor = condition.ConditionDetails.Sensor,
                        Threshold = condition.ConditionDetails.Value,
                        Duration = condition.ConditionDetails.Duration,
                    };
                default:
                    throw new NotImplementedException(
                        $"Unsupported condition type: {condition.ConditionDetails.Type}"
                    );
            }
        }

        private ComparisonOperator ParseOperator(string op)
        {
            return op switch
            {
                ">" => ComparisonOperator.GreaterThan,
                "<" => ComparisonOperator.LessThan,
                ">=" => ComparisonOperator.GreaterThanOrEqual,
                "<=" => ComparisonOperator.LessThanOrEqual,
                "==" => ComparisonOperator.EqualTo,
                "!=" => ComparisonOperator.NotEqualTo,
                _ => throw new InvalidOperationException($"Unsupported operator: {op}"),
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
                .Select(actionItem =>
                {
                    Debug.WriteLine($"Processing action item");

                    if (actionItem != null && actionItem.SetValue != null)
                    {
                        Debug.WriteLine($"Found SetValue action");
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
                        return setValueAction as ActionDefinition;
                    }

                    if (actionItem != null && actionItem.SendMessage != null)
                    {
                        Debug.WriteLine($"Found SendMessage action");
                        return new SendMessageAction
                            {
                                Type = ActionType.SendMessage,
                                Channel = actionItem.SendMessage.Channel,
                                Message = actionItem.SendMessage.Message,
                            } as ActionDefinition;
                    }

                    Debug.WriteLine($"No valid action type found");
                    throw new InvalidOperationException(
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
        public double Value { get; set; }
        public string Expression { get; set; } = string.Empty;
        public int Duration { get; set; }
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
        public double? Value { get; set; }

        [YamlMember(Alias = "value_expression")]
        public string? ValueExpression { get; set; }
    }

    public class SendMessageActionYaml
    {
        [YamlMember(Alias = "channel")]
        public string Channel { get; set; } = string.Empty;

        [YamlMember(Alias = "message")]
        public string Message { get; set; } = string.Empty;
    }
}
