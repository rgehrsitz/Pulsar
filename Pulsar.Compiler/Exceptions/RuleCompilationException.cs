// File: Pulsar.Compiler/Exceptions/RuleCompilationException.cs

using System;
using System.Collections.Generic;
using Serilog;

namespace Pulsar.Compiler.Exceptions
{
    public class RuleCompilationException : Exception
    {
        public string RuleName { get; }
        public string? RuleSource { get; }
        public int? LineNumber { get; }
        public string? ErrorType { get; }
        public Dictionary<string, object>? Context { get; }

        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        public RuleCompilationException(
            string message,
            string ruleName,
            string? ruleSource = null,
            int? lineNumber = null,
            string? errorType = "CompilationError",
            Dictionary<string, object>? context = null)
            : base(message)
        {
            RuleName = ruleName;
            RuleSource = ruleSource;
            LineNumber = lineNumber;
            ErrorType = errorType ?? "CompilationError";
            Context = context ?? new Dictionary<string, object>();

            LogError();
        }

        public RuleCompilationException(
            string message,
            string ruleName,
            Exception innerException,
            string? ruleSource = null,
            int? lineNumber = null,
            string? errorType = "CompilationError",
            Dictionary<string, object>? context = null)
            : base(message, innerException)
        {
            RuleName = ruleName;
            RuleSource = ruleSource;
            LineNumber = lineNumber;
            ErrorType = errorType ?? "CompilationError";
            Context = context ?? new Dictionary<string, object>();

            LogError();
        }

        private void LogError()
        {
            var errorContext = new Dictionary<string, object>(Context)
            {
                ["RuleName"] = RuleName,
                ["ErrorType"] = ErrorType
            };

            if (RuleSource != null)
                errorContext["RuleSource"] = RuleSource;

            if (LineNumber.HasValue)
                errorContext["LineNumber"] = LineNumber.Value;

            if (InnerException != null)
                errorContext["InnerError"] = InnerException.Message;

            _logger.Error(
                "Rule compilation error: {ErrorMessage} {@Context}",
                Message,
                errorContext
            );
        }

        public override string ToString()
        {
            var location = LineNumber.HasValue ? $" at line {LineNumber}" : "";
            var source = !string.IsNullOrEmpty(RuleSource) ? $" in {RuleSource}" : "";
            return $"{ErrorType} in rule '{RuleName}'{source}{location}: {Message}";
        }
    }
}
