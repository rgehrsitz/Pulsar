// File: Pulsar.Compiler/Models/RuleDefinition.cs

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Serilog;

namespace Pulsar.Compiler.Models
{
    public class RuleDefinition
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ConditionGroup? Conditions { get; set; }
        public List<ActionDefinition> Actions { get; set; } = new();
        public string SourceFile { get; set; } = string.Empty;
        public int LineNumber { get; set; }

        public void Validate()
        {
            try
            {
                _logger.Debug("Validating rule: {RuleName}", Name);

                if (string.IsNullOrEmpty(Name))
                {
                    _logger.Error("Rule name is required");
                    throw new ArgumentException("Rule name is required");
                }

                if (Conditions == null)
                {
                    _logger.Error("Rule {RuleName} must have conditions", Name);
                    throw new ArgumentException($"Rule {Name} must have conditions");
                }

                if (Actions.Count == 0)
                {
                    _logger.Error("Rule {RuleName} must have at least one action", Name);
                    throw new ArgumentException($"Rule {Name} must have at least one action");
                }

                _logger.Debug("Rule {RuleName} validation successful", Name);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Rule {RuleName} validation failed", Name);
                throw;
            }
        }

        public void LogInfo()
        {
            _logger.Information(
                "Rule: {Name} from {File}:{Line}, {ActionCount} actions",
                Name,
                SourceFile,
                LineNumber,
                Actions.Count
            );
        }
    }

    public class ConditionGroup : ConditionDefinition
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        public List<ConditionDefinition> All { get; set; } = new();
        public List<ConditionDefinition> Any { get; set; } = new();
        public ConditionGroup? Parent { get; private set; }

        public void AddToAll(ConditionDefinition condition)
        {
            if (condition is ConditionGroup group)
            {
                group.Parent = this;
            }
            All.Add(condition);
        }

        public void AddToAny(ConditionDefinition condition)
        {
            if (condition is ConditionGroup group)
            {
                group.Parent = this;
            }
            Any.Add(condition);
        }

        public override void Validate()
        {
            _logger.Debug("Validating condition group");
            if ((All == null || All.Count == 0) && (Any == null || Any.Count == 0))
            {
                _logger.Error("Condition group must have at least one condition in All or Any");
                throw new ArgumentException("Condition group must have at least one condition");
            }
        }
    }

    public class RuleGroup
    {
        public List<RuleDefinition> Rules { get; set; } = new();
    }

    public abstract class ConditionDefinition
    {
        [JsonIgnore]
        protected static readonly ILogger _logger = LoggingConfig.GetLogger();

        public ConditionType Type { get; set; }
        public SourceInfo? SourceInfo { get; set; }

        public abstract void Validate();
    }

    public enum ConditionType
    {
        Comparison,
        Expression,
        ThresholdOverTime,
        Group,
    }

    public class ComparisonCondition : ConditionDefinition
    {
        public new ConditionType Type { get; set; } = ConditionType.Comparison;
        public string Sensor { get; set; } = string.Empty;
        public ComparisonOperator Operator { get; set; }
        public double Value { get; set; }

        public override void Validate()
        {
            _logger.Debug("Validating comparison condition for sensor {Sensor}", Sensor);
            if (string.IsNullOrEmpty(Sensor))
            {
                _logger.Error("Comparison condition must specify a sensor");
                throw new ArgumentException("Sensor is required for comparison condition");
            }
        }
    }

    public enum ComparisonOperator
    {
        LessThan,
        GreaterThan,
        LessThanOrEqual,
        GreaterThanOrEqual,
        EqualTo,
        NotEqualTo,
    }

    public class ExpressionCondition : ConditionDefinition
    {
        public new ConditionType Type { get; set; } = ConditionType.Expression;
        public string Expression { get; set; } = string.Empty;

        public override void Validate()
        {
            _logger.Debug("Validating expression condition: {Expression}", Expression);
            if (string.IsNullOrEmpty(Expression))
            {
                _logger.Error("Expression condition must have an expression");
                throw new ArgumentException("Expression is required");
            }
        }
    }

    public class ThresholdOverTimeCondition : ConditionDefinition
    {
        public new ConditionType Type { get; set; } = ConditionType.ThresholdOverTime;
        public string Sensor { get; set; } = string.Empty;
        public double Threshold { get; set; }
        public int Duration { get; set; }
    }

    public abstract class ActionDefinition
    {
        [JsonIgnore]
        protected static readonly ILogger _logger = LoggingConfig.GetLogger();

        public ActionType Type { get; set; }
        public SourceInfo? SourceInfo { get; set; }

        public virtual void Validate()
        {
            _logger.Debug("Validating action of type {Type}", Type);
        }
    }

    public enum ActionType
    {
        SetValue,
        SendMessage,
    }

    public class SetValueAction : ActionDefinition
    {
        public new ActionType Type { get; set; } = ActionType.SetValue;
        public string Key { get; set; } = string.Empty;
        public double? Value { get; set; }
        public string? ValueExpression { get; set; }

        public override void Validate()
        {
            base.Validate();
            _logger.Debug("Validating SetValue action for key {Key}", Key);
            if (string.IsNullOrEmpty(Key))
            {
                _logger.Error("SetValue action must specify a key");
                throw new ArgumentException("Key is required for SetValue action");
            }
            if (string.IsNullOrEmpty(ValueExpression) && Value == 0)
            {
                _logger.Error("SetValue action must specify either Value or ValueExpression");
                throw new ArgumentException("Either Value or ValueExpression must be specified");
            }
        }
    }

    public class SendMessageAction : ActionDefinition
    {
        public new ActionType Type { get; set; } = ActionType.SendMessage;
        public string Channel { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public override void Validate()
        {
            base.Validate();
            _logger.Debug("Validating SendMessage action for channel {Channel}", Channel);
            if (string.IsNullOrEmpty(Channel))
            {
                _logger.Error("SendMessage action must specify a channel");
                throw new ArgumentException("Channel is required for SendMessage action");
            }
            if (string.IsNullOrEmpty(Message))
            {
                _logger.Error("SendMessage action must specify a message");
                throw new ArgumentException("Message is required for SendMessage action");
            }
        }
    }
}
