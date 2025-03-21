// File: Pulsar.Compiler/Exceptions/RuleCompilationException.cs

using Serilog;

namespace Pulsar.Compiler.Exceptions
{
    public class RuleCompilationException : Exception
    {
        public string RuleName { get; } = string.Empty; // Initialize to avoid CS8618
        public string? RuleSource { get; }
        public int? LineNumber { get; }
        public string? ErrorType { get; }
        public Dictionary<string, object>? Context { get; }
        public string? SourceCode { get; private set; } // Add missing SourceCode property

        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        public RuleCompilationException(
            string message,
            string ruleName,
            string? ruleSource = null,
            int? lineNumber = null,
            string? errorType = "CompilationError",
            Dictionary<string, object>? context = null
        )
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
            Dictionary<string, object>? context = null
        )
            : base(message, innerException)
        {
            RuleName = ruleName;
            RuleSource = ruleSource;
            LineNumber = lineNumber;
            ErrorType = errorType ?? "CompilationError";
            Context = context ?? new Dictionary<string, object>();

            LogError();
        }

        public RuleCompilationException(string message, IDictionary<string, object>? context = null)
            : base(message)
        {
            RuleName = string.Empty; // Set default for non-nullable property
            // Fix CS8604: Ensure context is not null before creating Dictionary
            Context =
                context != null
                    ? new Dictionary<string, object>(context)
                    : new Dictionary<string, object>();

            // Fix CS8601: Add proper null checks
            if (context != null && context.TryGetValue("SourceCode", out var sourceCode))
            {
                SourceCode = sourceCode?.ToString();
            }

            if (
                context != null
                && context.TryGetValue("RuleName", out var ruleName)
                && ruleName is string ruleName2
            )
            {
                RuleName = ruleName2;
            }

            // Set default for ErrorType
            ErrorType = "CompilationError";
        }

        private void LogError()
        {
            var errorContext = new Dictionary<string, object>(Context)
            {
                ["RuleName"] = RuleName,
                ["ErrorType"] = ErrorType,
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
