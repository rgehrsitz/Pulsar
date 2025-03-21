// File: Pulsar.Compiler/Models/CompilationError.cs

namespace Pulsar.Compiler.Models
{
    public class CompilationError(
        string message,
        string? fileName = null,
        int? lineNumber = null,
        string? ruleName = null,
        Exception? exception = null)
    {
        public string Message { get; } = message ?? throw new ArgumentNullException(nameof(message));
        public string? FileName { get; } = fileName;
        public int? LineNumber { get; } = lineNumber;
        public string? RuleName { get; } = ruleName;
        public Exception? Exception { get; } = exception;

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
