// File: Pulsar.Compiler/Models/CompilationError.cs
using System;

namespace Pulsar.Compiler.Models
{
    public class CompilationError
    {
        public string Message { get; }
        public string? FileName { get; }
        public int? LineNumber { get; }
        public string? RuleName { get; }
        public Exception? Exception { get; }

        public CompilationError(
            string message,
            string? fileName = null,
            int? lineNumber = null,
            string? ruleName = null,
            Exception? exception = null
        )
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            FileName = fileName;
            LineNumber = lineNumber;
            RuleName = ruleName;
            Exception = exception;
        }

        public override string ToString()
        {
            var location = FileName != null ? $" in {FileName}" : "";
            location += LineNumber.HasValue ? $" at line {LineNumber}" : "";
            var rule = RuleName != null ? $" (Rule: {RuleName})" : "";
            var error = Exception != null ? $"\nException: {Exception.Message}" : "";

            return $"Error{location}{rule}: {Message}{error}";
        }
    }
}
